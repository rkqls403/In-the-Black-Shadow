using System.Collections;
using UnityEngine;

namespace BlackShade
{
    // 1인칭 플레이어. 세 가지 상태가 있어요.
    //   Free     : 플레이어가 걷고 둘러볼 수 있음
    //   LookOnly : 제자리에서 둘러보기만 (대화 중, 앉아 있을 때)
    //   Cutscene : 연출 코드가 카메라를 직접 움직임
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public enum Mode { Free, LookOnly, Cutscene }

        [Header("이동")]
        public float walkSpeed = 2.2f;
        public float runSpeed = 3.4f;
        public float gravity = -15f;

        [Header("시점")]
        public float mouseSensitivity = 1.6f;
        public float standEyeHeight = 1.6f;
        public float minPitch = -80f, maxPitch = 80f;

        [Header("연출")]
        public float headBobAmount = 0.035f;
        public float stepInterval = 0.55f;

        public Mode mode = Mode.Cutscene;
        public Camera Cam { get; private set; }
        public Transform Head { get; private set; }

        // 현재 시점 값 (연출 코드가 직접 바꿔도 됨)
        public float yaw, pitch, roll;
        public float eyeHeight = 1.6f;
        public float fov = 65f;

        // LookOnly 모드에서 기준 방향으로부터 둘러볼 수 있는 범위
        public float lookOnlyYawRange = 70f;
        float lookOnlyCenterYaw;

        CharacterController cc;
        float verticalVel;
        float bobTime, stepTimer;
        float shakeAmount, shakeTime;
        public float MovedDistance { get; private set; }
        public float LookedDegrees { get; private set; }

        void Awake()
        {
            gameObject.layer = 2; // 괴물 시야/상호작용 판정에서 자기 몸은 무시
            cc = GetComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.24f;  // 책상 사이 통로가 좁아서 조금 얇게
            cc.center = new Vector3(0f, 0.875f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 50f;

            Head = new GameObject("Head").transform;
            Head.SetParent(transform, false);
            GameObject camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(Head, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();
        }

        public void SetMode(Mode m)
        {
            mode = m;
            cc.enabled = (m == Mode.Free);
            if (m == Mode.LookOnly) lookOnlyCenterYaw = yaw;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 순간이동 (발 위치 기준)
        public void Teleport(Vector3 feet, float newYaw)
        {
            bool was = cc.enabled;
            cc.enabled = false;
            transform.position = feet;
            yaw = newYaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cc.enabled = was;
            verticalVel = 0f;
        }

        void Update()
        {
            if (UIState.ModalOpen) return;
            // 창을 다른 곳에 클릭했다가 돌아왔을 때 다시 마우스 잠금
            if (GameInput.MouseClickDown() && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (mode == Mode.Free || mode == Mode.LookOnly)
            {
                Vector2 look = Cursor.lockState == CursorLockMode.Locked ? GameInput.Look() * mouseSensitivity : Vector2.zero;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
                roll = Mathf.Lerp(roll, 0f, Time.deltaTime * 5f);
                if (mode == Mode.LookOnly)
                {
                    float d = Mathf.DeltaAngle(lookOnlyCenterYaw, yaw);
                    yaw = lookOnlyCenterYaw + Mathf.Clamp(d, -lookOnlyYawRange, lookOnlyYawRange);
                    pitch = Mathf.Clamp(pitch, -45f, 50f);
                }
                LookedDegrees += look.magnitude;
            }

            if (mode == Mode.Free) Move();
        }

        void Move()
        {
            Vector2 input = GameInput.Move();
            bool run = GameInput.Run();
            Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 vel = (fwd * input.y + right * input.x) * (run ? runSpeed : walkSpeed);

            if (cc.isGrounded && verticalVel < 0f) verticalVel = -2f;
            verticalVel += gravity * Time.deltaTime;
            vel.y = verticalVel;

            Vector3 before = transform.position;
            cc.Move(vel * Time.deltaTime);
            Vector3 delta = transform.position - before;
            delta.y = 0f;
            MovedDistance += delta.magnitude;

            float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            if (speed > 0.3f && cc.isGrounded)
            {
                bobTime += Time.deltaTime * (run ? 11f : 8f);
                stepTimer -= Time.deltaTime * (run ? 1.4f : 1f);
                if (stepTimer <= 0f)
                {
                    stepTimer = stepInterval;
                    if (AudioDirector.I != null)
                        AudioDirector.I.Play(AudioDirector.I.RandomFootstep(), run ? 0.5f : 0.35f, Random.Range(0.9f, 1.1f));
                    // 괴물이 듣는다: 뛰면 멀리까지, 걸으면 가까이에서만
                    Noise.Emit(transform.position, run ? 11f : 3.5f, 1);
                }
            }
            else
            {
                bobTime = Mathf.Lerp(bobTime, Mathf.Round(bobTime / Mathf.PI) * Mathf.PI, Time.deltaTime * 6f);
            }
        }

        void LateUpdate()
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float bob = mode == Mode.Free ? Mathf.Sin(bobTime) * headBobAmount : 0f;

            Vector3 shake = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                float s = shakeAmount * Mathf.Clamp01(shakeTime * 3f);
                shake = new Vector3(Random.Range(-s, s), Random.Range(-s, s), 0f);
            }

            Head.localPosition = new Vector3(0f, eyeHeight + bob, 0f) + shake;
            Head.localRotation = Quaternion.Euler(pitch, 0f, roll);
            Cam.fieldOfView = fov;
        }

        public void Shake(float amount, float duration)
        {
            shakeAmount = amount;
            shakeTime = duration;
        }

        // 카메라 위치(눈)에서 target을 바라보는 yaw/pitch 계산
        public void AnglesTo(Vector3 target, out float y, out float p)
        {
            Vector3 eye = Head.position;
            Vector3 d = target - eye;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            y = flat.sqrMagnitude < 0.0001f ? yaw : Quaternion.LookRotation(flat).eulerAngles.y;
            p = -Mathf.Atan2(d.y, Mathf.Max(flat.magnitude, 0.0001f)) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------ 연출용 트윈
        static float Smooth(float t) { return t * t * (3f - 2f * t); }

        // 발 위치/시점/눈높이/기울기를 한 번에 부드럽게 바꾼다
        public IEnumerator TweenView(Vector3 feet, float toYaw, float toPitch, float toRoll, float toEye, float duration)
        {
            Vector3 p0 = transform.position;
            float y0 = yaw, pi0 = pitch, r0 = roll, e0 = eyeHeight;
            bool was = cc.enabled;
            cc.enabled = false;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Smooth(Mathf.Clamp01(t / duration));
                transform.position = Vector3.Lerp(p0, feet, k);
                yaw = Mathf.LerpAngle(y0, toYaw, k);
                pitch = Mathf.Lerp(pi0, toPitch, k);
                roll = Mathf.LerpAngle(r0, toRoll, k);
                eyeHeight = Mathf.Lerp(e0, toEye, k);
                yield return null;
            }
            cc.enabled = was && mode == Mode.Free;
        }

        // 제자리에서 어떤 지점을 바라보도록 고개를 돌린다
        public IEnumerator LookAtPoint(Vector3 target, float duration)
        {
            float ty, tp;
            AnglesTo(target, out ty, out tp);
            float y0 = yaw, p0 = pitch, r0 = roll;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Smooth(Mathf.Clamp01(t / duration));
                AnglesTo(target, out ty, out tp);
                yaw = Mathf.LerpAngle(y0, ty, k);
                pitch = Mathf.Lerp(p0, tp, k);
                roll = Mathf.LerpAngle(r0, 0f, k);
                yield return null;
            }
            if (mode == Mode.LookOnly) lookOnlyCenterYaw = yaw;
        }

        public IEnumerator TweenFov(float to, float duration)
        {
            float f0 = fov, t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                fov = Mathf.Lerp(f0, to, Smooth(Mathf.Clamp01(t / duration)));
                yield return null;
            }
        }
    }
}
