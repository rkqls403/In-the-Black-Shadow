using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BlackShade
{
    // 소리 알림 (발소리, 빈 캔). 괴물이 듣는다.
    public static class Noise
    {
        public static System.Action<Vector3, float, int> OnNoise;
        // radius: 들리는 거리, priority: 1 = 발소리, 2 = 빈 캔(쫓던 것도 잠깐 잊고 그쪽으로 감)
        public static void Emit(Vector3 pos, float radius, int priority)
        {
            if (OnNoise != null) OnNoise(pos, radius, priority);
        }
    }

    // 선생님 괴물.
    //   배회 → (소리) 확인하러 감 → (발견) 추격 → (가까우면) 공격
    //   못 찾으면 마지막 위치 주변을 찾다가 다시 배회.
    //   플레이어가 다른 층으로 가면 잠시 뒤 계단으로 따라온다.
    public class TeacherMonster : MonoBehaviour
    {
        public static TeacherMonster I { get; private set; }

        enum State { Idle, Patrol, Investigate, Chase, Search, Attack, CheckHide, Stunned }

        [Header("속도")]
        public float patrolSpeed = 1.25f;
        public float investigateSpeed = 2.0f;
        public float chaseSpeed = 3.05f;

        [Header("감각")]
        public float sightRange = 13f;
        public float sightRangeLit = 19f;       // 플레이어가 손전등을 켰거나 밝은 곳에 있으면
        public float fov = 115f;
        public float attackRange = 1.55f;
        public float damage = 34f;

        public bool Active { get; private set; }
        public float DistanceToPlayer { get; private set; }

        State state = State.Idle;
        NavMeshAgent agent;
        bool useNav;
        Transform body, head, armL, armR;
        Vector3 goal, lastSeen;
        float stateTime, lostTime, repathTime, stepTimer, otherFloorTime, blindTime, seenRecently;
        HideSpot targetHide;
        Vector3[] stairSpots;
        FirstPersonController player;
        CharacterController fallbackCc;
        float bob;

        // ---------------------------------------------------------------- 만들기
        public static TeacherMonster Create(GameObject modelPrefab, FirstPersonController player)
        {
            GameObject go = new GameObject("TeacherMonster");
            go.layer = 2; // Ignore Raycast (시야 판정에서 자기 몸은 무시)
            TeacherMonster m = go.AddComponent<TeacherMonster>();
            m.player = player;
            m.BuildBody(modelPrefab);
            I = m;
            Noise.OnNoise += m.Hear;
            go.SetActive(false);
            return m;
        }

        void OnDestroy() { Noise.OnNoise -= Hear; if (I == this) I = null; }

        void BuildBody(GameObject prefab)
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            if (prefab != null)
            {
                GameObject m = Instantiate(prefab, body);
                foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
                Bounds b;
                if (ClassroomFrame.TryBounds(m.transform, out b) && b.size.y > 0.01f)
                {
                    m.transform.localScale *= 2.25f / b.size.y;
                    ClassroomFrame.TryBounds(m.transform, out b);
                    m.transform.position += new Vector3(transform.position.x - b.center.x, transform.position.y - b.min.y, transform.position.z - b.center.z);
                }
                head = m.transform;
            }
            else
            {
                // 임시 모습: 여러 선생님이 뒤엉킨 듯한 길쭉한 형체
                Color suit = new Color(0.08f, 0.08f, 0.09f);
                Color skin = new Color(0.62f, 0.6f, 0.56f);
                Transform t = body;
                Prop.Shape(PrimitiveType.Capsule, t, "Torso", new Vector3(0f, 1.25f, 0f), Quaternion.Euler(12f, 0, 0), new Vector3(0.55f, 0.75f, 0.38f), suit, false);
                Prop.Shape(PrimitiveType.Capsule, t, "Torso2", new Vector3(0.12f, 1.05f, -0.1f), Quaternion.Euler(-8f, 0, 14f), new Vector3(0.5f, 0.6f, 0.34f), suit * 1.3f, false);
                Prop.Shape(PrimitiveType.Capsule, t, "LegL", new Vector3(-0.15f, 0.42f, 0f), Quaternion.identity, new Vector3(0.18f, 0.45f, 0.18f), suit, false);
                Prop.Shape(PrimitiveType.Capsule, t, "LegR", new Vector3(0.15f, 0.42f, 0f), Quaternion.identity, new Vector3(0.18f, 0.45f, 0.18f), suit, false);
                head = new GameObject("Head").transform;
                head.SetParent(t, false);
                head.localPosition = new Vector3(0f, 2.0f, 0.18f);
                head.localRotation = Quaternion.Euler(25f, 0f, -18f);
                Prop.Shape(PrimitiveType.Sphere, head, "Face", head.position, head.rotation, new Vector3(0.27f, 0.34f, 0.27f), skin, false);
                GameObject eyeL = Prop.Shape(PrimitiveType.Sphere, head, "EyeL", head.position + head.rotation * new Vector3(-0.06f, 0.03f, 0.12f), head.rotation, new Vector3(0.05f, 0.03f, 0.03f), Color.black, false);
                GameObject eyeR = Prop.Shape(PrimitiveType.Sphere, head, "EyeR", head.position + head.rotation * new Vector3(0.06f, 0.03f, 0.12f), head.rotation, new Vector3(0.05f, 0.03f, 0.03f), Color.black, false);
                Prop.SetEmission(eyeL.GetComponent<Renderer>(), new Color(0.5f, 0.05f, 0.03f));
                Prop.SetEmission(eyeR.GetComponent<Renderer>(), new Color(0.5f, 0.05f, 0.03f));
                // 두 번째 머리 (어깨에 파묻힌)
                Prop.Shape(PrimitiveType.Sphere, t, "Head2", new Vector3(0.28f, 1.68f, -0.05f), Quaternion.Euler(0, 40, 70), new Vector3(0.22f, 0.27f, 0.22f), skin * 0.85f, false);
                // 너무 긴 팔들
                armL = new GameObject("ArmL").transform; armL.SetParent(t, false); armL.localPosition = new Vector3(-0.32f, 1.6f, 0.05f);
                armR = new GameObject("ArmR").transform; armR.SetParent(t, false); armR.localPosition = new Vector3(0.32f, 1.6f, 0.05f);
                Prop.Shape(PrimitiveType.Capsule, armL, "Arm", armL.position + Vector3.down * 0.65f, Quaternion.Euler(0, 0, -6f), new Vector3(0.11f, 0.68f, 0.11f), suit, false);
                Prop.Shape(PrimitiveType.Capsule, armR, "Arm", armR.position + Vector3.down * 0.65f, Quaternion.Euler(0, 0, 6f), new Vector3(0.11f, 0.68f, 0.11f), suit, false);
                Prop.Shape(PrimitiveType.Sphere, armL, "Hand", armL.position + Vector3.down * 1.32f, Quaternion.identity, new Vector3(0.12f, 0.16f, 0.08f), skin, false);
                Prop.Shape(PrimitiveType.Sphere, armR, "Hand", armR.position + Vector3.down * 1.32f, Quaternion.identity, new Vector3(0.12f, 0.16f, 0.08f), skin, false);
                // 등 뒤로 튀어나온 또 다른 팔
                Prop.Shape(PrimitiveType.Capsule, t, "ExtraArm", new Vector3(-0.1f, 1.5f, -0.3f), Quaternion.Euler(-60f, 30f, 0), new Vector3(0.09f, 0.5f, 0.09f), suit, false);
                // 낡은 명찰
                GameObject badge = Prop.Shape(PrimitiveType.Cube, t, "Badge", new Vector3(-0.15f, 1.45f, 0.21f), Quaternion.Euler(12f, 0, 0), new Vector3(0.08f, 0.03f, 0.01f), new Color(0.85f, 0.4f, 0.55f), false);
                Prop.SetEmission(badge.GetComponent<Renderer>(), new Color(0.2f, 0.05f, 0.08f));
            }
            foreach (Transform c in GetComponentsInChildren<Transform>(true)) c.gameObject.layer = 2;
        }

        // ---------------------------------------------------------------- 켜기 / 끄기
        public void SetStairSpots(Vector3[] spots) { stairSpots = spots; }

        public void Activate(Vector3 at, bool chaseNow)
        {
            if (DevMode.MonsterOff) return; // 개발자 모드에서 꺼 둔 경우
            gameObject.SetActive(true);
            Active = true;
            Warp(at);
            AudioDirector.I.StartLoop("murmur", AudioDirector.I.murmur, 0.55f, transform.position, 1.5f);
            if (chaseNow) { lastSeen = player.transform.position; Enter(State.Chase); }
            else Enter(State.Patrol);
        }

        public void Deactivate()
        {
            Active = false;
            AudioDirector.I.StopLoop("murmur", 1f);
            AudioDirector.I.StopLoop("heart", 0.5f);
            gameObject.SetActive(false);
        }

        void Warp(Vector3 at)
        {
            if (agent == null)
            {
                transform.position = at; // 길찾기 컴포넌트를 붙이기 전에 길 위로 옮겨 둠
                agent = gameObject.AddComponent<NavMeshAgent>();
                agent.radius = 0.28f;
                agent.height = 2.1f;
                agent.acceleration = 14f;
                agent.angularSpeed = 300f;
                agent.stoppingDistance = 0.3f;
                agent.autoBraking = true;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            }
            NavMeshHit hit;
            useNav = NavMesh.SamplePosition(at, out hit, 2.5f, NavMesh.AllAreas);
            if (useNav)
            {
                agent.enabled = true;
                agent.Warp(hit.position);
            }
            else
            {
                // 내비메시가 없으면 단순히 직진 (벽에 막힘)
                agent.enabled = false;
                transform.position = at;
                if (fallbackCc == null)
                {
                    fallbackCc = gameObject.AddComponent<CharacterController>();
                    fallbackCc.radius = 0.28f; fallbackCc.height = 2f; fallbackCc.center = Vector3.up;
                    Debug.LogWarning("[TeacherMonster] NavMesh가 없어요. 'Tools > Black Shade > 길찾기(NavMesh) 다시 굽기'를 실행하세요.");
                }
            }
        }

        // ---------------------------------------------------------------- 감각
        Vector3 Eye { get { return transform.position + Vector3.up * 1.95f; } }

        public bool CanSeePlayerNow()
        {
            if (HideSpot.PlayerHidden) return false;
            Vector3 target = player.Head.position;
            Vector3 d = target - Eye;
            float dist = d.magnitude;
            bool lit = (Flashlight.I != null && Flashlight.I.On) || RoomLights.IsLitAt(player.transform.position);
            float range = lit ? sightRange * 1f + (sightRangeLit - sightRange) : sightRange;
            if (dist < 2.2f) range = 99f; // 바로 옆은 무조건 느낌
            if (dist > range) return false;
            if (dist > 2.2f && Vector3.Angle(transform.forward, d) > fov * 0.5f) return false;
            if (Mathf.Abs(MapSpace.FloorOf(transform.position) - MapSpace.FloorOf(player.transform.position)) > 0) return false;
            RaycastHit hit;
            if (Physics.Raycast(Eye, d / dist, out hit, dist - 0.2f, ~(1 << 2), QueryTriggerInteraction.Ignore)) return false;
            return true;
        }

        void Hear(Vector3 pos, float radius, int priority)
        {
            if (!Active || state == State.Attack || state == State.Stunned) return;
            if ((pos - transform.position).sqrMagnitude > radius * radius) return;
            if (Mathf.Abs(pos.y - transform.position.y) > 2.5f) return;
            if (priority >= 2)
            {
                // 빈 캔: 쫓던 중이라도 소리 난 곳으로
                blindTime = 3.5f;
                goal = pos;
                Enter(State.Investigate);
                return;
            }
            if (state == State.Chase) return;
            goal = pos;
            Enter(State.Investigate);
        }

        // 연출용: 특정 위치로 확인하러 가게 함
        public void Investigate(Vector3 pos)
        {
            goal = pos;
            Enter(State.Investigate);
        }

        // ---------------------------------------------------------------- 상태
        void Enter(State s)
        {
            state = s;
            stateTime = 0f;
            repathTime = 0f;
            if (s == State.Patrol) PickPatrolGoal();
            if (s == State.Chase) MainDirector.I.OnChaseStarted();
        }

        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            stateTime += dt;
            blindTime -= dt;
            DistanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

            bool sees = blindTime <= 0f && CanSeePlayerNow();
            if (sees) { lastSeen = player.transform.position; seenRecently = 1.2f; }
            else seenRecently -= dt;

            switch (state)
            {
                case State.Patrol:
                    SetSpeed(patrolSpeed);
                    if (sees) { Enter(State.Chase); break; }
                    if (Arrived() || stateTime > 25f) PickPatrolGoal();
                    FollowPlayerFloor(dt);
                    break;

                case State.Investigate:
                    SetSpeed(investigateSpeed);
                    if (sees) { Enter(State.Chase); break; }
                    MoveTo(goal);
                    if (Arrived() || stateTime > 14f) { lastSeen = goal; Enter(State.Search); }
                    break;

                case State.Chase:
                    SetSpeed(chaseSpeed);
                    if (HideSpot.PlayerHidden)
                    {
                        HideSpot h = HideSpot.Current;
                        if (h.SeenEntering && DistanceToPlayer < 9f) { targetHide = h; Enter(State.CheckHide); break; }
                        Enter(State.Search);
                        break;
                    }
                    if (sees) lostTime = 0f; else lostTime += dt;
                    MoveTo(sees ? player.transform.position : lastSeen);
                    if (sees && DistanceToPlayer < attackRange) { Enter(State.Attack); break; }
                    if (lostTime > 5f) Enter(State.Search);
                    break;

                case State.Search:
                    SetSpeed(investigateSpeed * 0.8f);
                    if (sees) { Enter(State.Chase); break; }
                    if (Arrived() || repathTime <= 0f)
                    {
                        repathTime = 3f;
                        Vector3 p;
                        if (RandomPointNear(lastSeen, 5f, out p)) MoveTo(p, true);
                    }
                    repathTime -= dt;
                    if (stateTime > 12f) Enter(State.Patrol);
                    break;

                case State.CheckHide:
                    SetSpeed(chaseSpeed * 0.8f);
                    if (targetHide == null || HideSpot.Current != targetHide) { Enter(State.Search); break; }
                    MoveTo(targetHide.FrontPoint);
                    if (Vector3.Distance(transform.position, targetHide.FrontPoint) < 1.1f)
                    {
                        Face(targetHide.transform.position);
                        targetHide.Yank();
                        MainDirector.I.DamagePlayer(damage, transform.position);
                        targetHide = null;
                        Enter(State.Stunned);
                    }
                    break;

                case State.Attack:
                    Stop();
                    Face(player.transform.position);
                    // 팔을 치켜드는 시간 (이때 도망칠 수 있음)
                    if (stateTime > 0.55f)
                    {
                        if (DistanceToPlayer < attackRange + 0.5f && !HideSpot.PlayerHidden)
                            MainDirector.I.DamagePlayer(damage, transform.position);
                        Enter(State.Stunned);
                    }
                    break;

                case State.Stunned:
                    Stop();
                    if (stateTime > 1.6f) Enter(sees ? State.Chase : State.Search);
                    break;
            }

            Animate(dt);
            Sounds(dt);
        }

        // 다른 층에 오래 있으면 계단으로 따라감 (안 보이는 쪽 계단에서 나타남)
        void FollowPlayerFloor(float dt)
        {
            int mf = MapSpace.FloorOf(transform.position), pf = MapSpace.FloorOf(player.transform.position);
            if (mf == pf) { otherFloorTime = 0f; return; }
            otherFloorTime += dt;
            if (otherFloorTime < 18f || stairSpots == null) return;
            otherFloorTime = 0f;
            Vector3 best = Vector3.zero; float bestD = -1f;
            foreach (Vector3 s in stairSpots)
            {
                Vector3 at = new Vector3(s.x, MapSpace.FloorY(pf), s.z);
                float d = Vector3.Distance(at, player.transform.position);
                if (d > bestD) { bestD = d; best = at; }
            }
            Warp(best);
            AudioDirector.I.PlayAt(AudioDirector.I.RandomHeavyStep(), best, 1f, 0.8f);
            PickPatrolGoal();
        }

        void PickPatrolGoal()
        {
            // 플레이어 근처를 어슬렁거림 (너무 가깝지도 멀지도 않게)
            Vector3 p;
            Vector3 center = player.transform.position;
            if (MapSpace.FloorOf(center) != MapSpace.FloorOf(transform.position)) center = transform.position;
            for (int i = 0; i < 8; i++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(6f, 20f);
                if (RandomPointNear(center + new Vector3(r.x, 0f, r.y), 3f, out p)) { MoveTo(p, true); return; }
            }
        }

        bool RandomPointNear(Vector3 c, float radius, out Vector3 p)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(c + new Vector3(r.x, 0f, r.y), out hit, 2.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - c.y) < 1.2f)
            { p = hit.position; return true; }
            p = c; return false;
        }

        void SetSpeed(float s) { if (useNav && agent.enabled) agent.speed = s; curSpeed = s; }
        float curSpeed;

        void MoveTo(Vector3 p, bool force = false)
        {
            if (useNav && agent.enabled)
            {
                repathTime -= Time.deltaTime;
                if (force || repathTime <= 0f || (agent.destination - p).sqrMagnitude > 1f)
                {
                    agent.isStopped = false;
                    agent.SetDestination(p);
                    if (!force) repathTime = 0.25f;
                }
            }
            else
            {
                goalFallback = p;
                Vector3 d = p - transform.position; d.y = 0f;
                if (d.magnitude > 0.3f)
                {
                    Face(p);
                    fallbackCc.Move((d.normalized * curSpeed + Vector3.down * 3f) * Time.deltaTime);
                }
            }
        }
        Vector3 goalFallback;

        void Stop()
        {
            if (useNav && agent.enabled) agent.isStopped = true;
        }

        bool Arrived()
        {
            if (useNav && agent.enabled) return !agent.pathPending && agent.remainingDistance < 0.6f;
            Vector3 d = goalFallback - transform.position; d.y = 0f;
            return d.magnitude < 0.6f;
        }

        void Face(Vector3 p)
        {
            Vector3 d = p - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 8f);
        }

        // ---------------------------------------------------------------- 움직임 / 소리
        void Animate(float dt)
        {
            float v = useNav && agent.enabled ? agent.velocity.magnitude : curSpeed;
            bob += dt * (2.5f + v * 2.2f);
            body.localPosition = new Vector3(Mathf.Sin(bob * 0.5f) * 0.04f, Mathf.Abs(Mathf.Sin(bob)) * 0.05f, 0f);
            body.localRotation = Quaternion.Euler(6f + v * 4f, Mathf.Sin(bob * 0.5f) * 6f, Mathf.Sin(bob * 0.37f) * 5f);
            if (armL != null)
            {
                float lift = state == State.Attack ? Mathf.Clamp01(stateTime / 0.5f) * 150f : 0f;
                armL.localRotation = Quaternion.Euler(Mathf.Sin(bob) * 18f - lift, 0f, -4f);
                armR.localRotation = Quaternion.Euler(-Mathf.Sin(bob) * 18f - lift * 0.8f, 0f, 4f);
            }
            if (head != null && state == State.Chase) head.localRotation = Quaternion.Euler(20f + Mathf.Sin(Time.time * 17f) * 6f, Mathf.Sin(Time.time * 11f) * 10f, -18f);
        }

        void Sounds(float dt)
        {
            AudioDirector a = AudioDirector.I;
            a.MoveLoop("murmur", transform.position + Vector3.up * 1.8f);
            float v = useNav && agent.enabled ? agent.velocity.magnitude : (state == State.Attack || state == State.Stunned ? 0f : curSpeed);
            if (v > 0.2f)
            {
                stepTimer -= dt * Mathf.Lerp(0.8f, 1.8f, v / chaseSpeed);
                if (stepTimer <= 0f)
                {
                    stepTimer = 0.75f;
                    a.PlayAt(a.RandomHeavyStep(), transform.position, Mathf.Lerp(0.6f, 1f, v / chaseSpeed), Random.Range(0.9f, 1.05f));
                }
            }
            // 가까우면 심장 소리
            bool near = DistanceToPlayer < 11f && MapSpace.FloorOf(transform.position) == MapSpace.FloorOf(player.transform.position);
            if (near) a.StartLoop("heart", a.heartbeat, Mathf.Lerp(0.6f, 0.15f, DistanceToPlayer / 11f), null, 0.4f);
            else a.StopLoop("heart", 1f);
        }

        public bool IsChasing { get { return Active && (state == State.Chase || state == State.Attack || state == State.CheckHide); } }
    }
}
