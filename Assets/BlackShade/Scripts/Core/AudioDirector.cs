using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // 효과음 재생 담당. 칸을 비워 두면 코드로 만든 임시 소리를 씁니다.
    // 진짜 효과음 파일이 생기면 인스펙터에서 해당 칸에 끌어다 놓으세요.
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector I { get; private set; }

        [Header("비워 두면 임시 합성음 사용")]
        public AudioClip fluorescentHum;
        public AudioClip roomTone;
        public AudioClip wind;
        public AudioClip chimeUp;
        public AudioClip chimeDown;
        public AudioClip staticNoise;
        public AudioClip powerCut;
        public AudioClip thud;
        public AudioClip chalk;
        public AudioClip inflate;
        public AudioClip pop;
        public AudioClip paper;
        public AudioClip sting;
        public AudioClip tick;
        public AudioClip[] footsteps;

        [Header("본편 (비워 두면 임시 합성음 사용)")]
        public AudioClip doorCreak;
        public AudioClip doorSlide;
        public AudioClip rattle;
        public AudioClip unlock;
        public AudioClip cabinet;
        public AudioClip heartbeat;
        public AudioClip murmur;
        public AudioClip hit;
        public AudioClip canClatter;
        public AudioClip vending;
        public AudioClip gulp;
        public AudioClip click;
        public AudioClip knock;
        public AudioClip shutter;
        public AudioClip pickup;
        public AudioClip saveChime;
        public AudioClip drone;
        public AudioClip droneRed;
        public AudioClip screams;
        public AudioClip graduationSong;   // 비우면 코드로 만든 졸업가
        public AudioClip[] heavySteps;

        [Range(0f, 1f)] public float masterVolume = 1f;

        readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<AudioSource, Coroutine> fades = new Dictionary<AudioSource, Coroutine>();

        void Fade(AudioSource s, float target, float time, bool stop)
        {
            Coroutine c;
            if (fades.TryGetValue(s, out c) && c != null) StopCoroutine(c);
            fades[s] = StartCoroutine(FadeVolume(s, target, time, stop));
        }

        void Awake()
        {
            I = this;
            if (fluorescentHum == null) fluorescentHum = SynthClips.FluorescentHum();
            if (roomTone == null) roomTone = SynthClips.RoomTone();
            if (wind == null) wind = SynthClips.Wind();
            if (chimeUp == null) chimeUp = SynthClips.Chime(true);
            if (chimeDown == null) chimeDown = SynthClips.Chime(false);
            if (staticNoise == null) staticNoise = SynthClips.Static(1.6f);
            if (powerCut == null) powerCut = SynthClips.PowerCut();
            if (thud == null) thud = SynthClips.Thud();
            if (chalk == null) chalk = SynthClips.Chalk(2.6f);
            if (inflate == null) inflate = SynthClips.Inflate(1.8f);
            if (pop == null) pop = SynthClips.Pop();
            if (paper == null) paper = SynthClips.Paper();
            if (sting == null) sting = SynthClips.Sting();
            if (tick == null) tick = SynthClips.Tick();
            if (doorCreak == null) doorCreak = SynthMore.DoorCreak();
            if (doorSlide == null) doorSlide = SynthMore.DoorSlide();
            if (rattle == null) rattle = SynthMore.Rattle();
            if (unlock == null) unlock = SynthMore.Unlock();
            if (cabinet == null) cabinet = SynthMore.Cabinet();
            if (heartbeat == null) heartbeat = SynthMore.Heartbeat();
            if (murmur == null) murmur = SynthMore.Murmur();
            if (hit == null) hit = SynthMore.Hit();
            if (canClatter == null) canClatter = SynthMore.CanClatter();
            if (vending == null) vending = SynthMore.Vending();
            if (gulp == null) gulp = SynthMore.Gulp();
            if (click == null) click = SynthMore.Click();
            if (knock == null) knock = SynthMore.Knock();
            if (shutter == null) shutter = SynthMore.Shutter();
            if (pickup == null) pickup = SynthMore.Pickup(false);
            if (saveChime == null) saveChime = SynthMore.Pickup(true);
            if (drone == null) drone = SynthMore.Drone(false);
            if (droneRed == null) droneRed = SynthMore.Drone(true);
            if (heavySteps == null || heavySteps.Length == 0)
                heavySteps = new[] { SynthMore.HeavyStep(61), SynthMore.HeavyStep(62), SynthMore.HeavyStep(63) };
            if (footsteps == null || footsteps.Length == 0)
                footsteps = new[] { SynthClips.Footstep(1), SynthClips.Footstep(2), SynthClips.Footstep(3), SynthClips.Footstep(4) };
        }

        AudioSource GetSource()
        {
            for (int i = 0; i < pool.Count; i++)
                if (!pool[i].isPlaying) return pool[i];
            GameObject go = new GameObject("SFX");
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            pool.Add(s);
            return s;
        }

        public AudioSource Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return null;
            AudioSource s = GetSource();
            s.transform.localPosition = Vector3.zero;
            s.spatialBlend = 0f;
            s.loop = false;
            s.clip = clip;
            s.volume = volume * masterVolume;
            s.pitch = pitch;
            s.Play();
            return s;
        }

        public AudioSource PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return null;
            AudioSource s = GetSource();
            s.transform.position = position;
            s.spatialBlend = 1f;
            s.minDistance = 2f;
            s.maxDistance = 30f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.loop = false;
            s.clip = clip;
            s.volume = volume * masterVolume;
            s.pitch = pitch;
            s.Play();
            return s;
        }

        public void StartLoop(string id, AudioClip clip, float volume, Vector3? position = null, float fadeIn = 0.5f)
        {
            if (clip == null) return;
            AudioSource s;
            if (!loops.TryGetValue(id, out s) || s == null)
            {
                GameObject go = new GameObject("Loop_" + id);
                go.transform.SetParent(transform, false);
                s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                loops[id] = s;
            }
            if (position.HasValue)
            {
                s.transform.position = position.Value;
                s.spatialBlend = 1f;
                s.minDistance = 3f;
                s.maxDistance = 25f;
            }
            else s.spatialBlend = 0f;
            s.loop = true;
            if (s.clip != clip) { s.Stop(); }
            s.clip = clip;
            if (!s.isPlaying) { s.volume = 0f; s.Play(); }
            Fade(s, volume * masterVolume, fadeIn, false);
        }

        public void StopLoop(string id, float fadeOut = 0.3f)
        {
            AudioSource s;
            if (loops.TryGetValue(id, out s) && s != null)
                Fade(s, 0f, fadeOut, true);
        }

        IEnumerator FadeVolume(AudioSource s, float target, float time, bool stopAtEnd)
        {
            float from = s.volume;
            float t = 0f;
            while (t < time && s != null)
            {
                t += Time.deltaTime;
                s.volume = Mathf.Lerp(from, target, time <= 0f ? 1f : t / time);
                yield return null;
            }
            if (s == null) yield break;
            s.volume = target;
            if (stopAtEnd) s.Stop();
        }

        // 졸업가와 비명은 길어서 필요할 때 만든다
        public AudioClip Song() { if (graduationSong == null) graduationSong = SynthMore.GraduationSong(); return graduationSong; }
        public AudioClip Screams() { if (screams == null) screams = SynthMore.Screams(); return screams; }
        public AudioClip RandomHeavyStep() { return heavySteps[Random.Range(0, heavySteps.Length)]; }

        public void StopAllLoops(float fade = 0.3f)
        {
            foreach (var kv in loops) if (kv.Value != null) Fade(kv.Value, 0f, fade, true);
        }

        // 위치가 있는 루프 소리를 옮긴다 (괴물 웅얼거림 등)
        public void MoveLoop(string id, Vector3 pos)
        {
            AudioSource s;
            if (loops.TryGetValue(id, out s) && s != null) s.transform.position = pos;
        }

        public void SetLoopVolume(string id, float v)
        {
            AudioSource s;
            if (loops.TryGetValue(id, out s) && s != null) s.volume = v * masterVolume;
        }

        public AudioClip RandomFootstep()
        {
            return footsteps[Random.Range(0, footsteps.Length)];
        }
    }
}
