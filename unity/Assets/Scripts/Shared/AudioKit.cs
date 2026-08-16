using UnityEngine;

namespace Tanker
{
    /// 절차 생성 사운드 — 외부 오디오 에셋 없이 칩튠 BGM·효과음을 코드로 합성한다.
    /// 클립은 최초 1회만 생성해 정적 캐시 (규약: 런타임 반복 생성 금지).
    /// 볼륨은 PlayerPrefs("vol.bgm"/"vol.sfx") 0~1.
    public static class AudioKit
    {
        const int RATE = 44100;

        static AudioSource bgmSrc, sfxSrc;
        static AudioClip bgmClip;
        static AudioClip clickClip, cardClip, hitClip, guardClip, healClip, winClip, loseClip;
        static float bgmVol = -1f, sfxVol = -1f;

        public static float BgmVolume
        {
            get { if (bgmVol < 0) bgmVol = PlayerPrefs.GetFloat("vol.bgm", 0.6f); return bgmVol; }
            set
            {
                bgmVol = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat("vol.bgm", bgmVol);
                if (bgmSrc != null) bgmSrc.volume = bgmVol * 0.5f; // BGM은 절반 스케일 — 은은하게
            }
        }

        public static float SfxVolume
        {
            get { if (sfxVol < 0) sfxVol = PlayerPrefs.GetFloat("vol.sfx", 0.8f); return sfxVol; }
            set { sfxVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat("vol.sfx", sfxVol); }
        }

        static volatile float[] pendingBgm; // 워커 스레드가 채우고 호스트 Update가 회수
        static bool bgmRequested;

        static void Ensure()
        {
            if (bgmSrc != null) return;
            var go = new GameObject("AudioKit");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<AudioKitHost>();
            bgmSrc = go.AddComponent<AudioSource>();
            sfxSrc = go.AddComponent<AudioSource>();
            bgmSrc.loop = true;
            bgmSrc.volume = BgmVolume * 0.5f;
        }

        /// BGM 샘플(~21초)은 워커 스레드에서 합성 — 첫 화면 메인 스레드 정지 방지.
        /// AudioClip 생성은 메인 스레드 전용이라 호스트가 완성 버퍼를 회수해 마무리한다.
        public static void PlayBgm()
        {
            Ensure();
            if (bgmClip != null)
            {
                if (bgmSrc.clip != bgmClip) { bgmSrc.clip = bgmClip; bgmSrc.Play(); }
                else if (!bgmSrc.isPlaying) bgmSrc.Play();
                return;
            }
            if (bgmRequested) return;
            bgmRequested = true;
            System.Threading.Tasks.Task.Run(() => { pendingBgm = MakeBgmSamples(); });
        }

        internal static void PollBgm()
        {
            var samples = pendingBgm;
            if (samples == null || bgmClip != null) return;
            pendingBgm = null;
            bgmClip = Clip("bgm", samples);
            bgmSrc.clip = bgmClip;
            bgmSrc.Play();
        }

        // ---- 효과음 재생 (지연 합성 + 캐시, 호출당 할당 없음) ----

        public static void Click() { Ensure(); PlayOne(clickClip ??= MakeClick()); }
        public static void Card() { Ensure(); PlayOne(cardClip ??= MakeCard()); }
        public static void Hit() { Ensure(); PlayOne(hitClip ??= MakeHit()); }
        public static void Guard() { Ensure(); PlayOne(guardClip ??= MakeGuard()); }
        public static void Heal() { Ensure(); PlayOne(healClip ??= MakeHeal()); }
        public static void Win() { Ensure(); PlayOne(winClip ??= MakeWin()); }
        public static void Lose() { Ensure(); PlayOne(loseClip ??= MakeLose()); }

        static void PlayOne(AudioClip clip) => sfxSrc.PlayOneShot(clip, SfxVolume);

        // ---- 합성 유틸 ----

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, RATE, false);
            c.SetData(data, 0);
            return c;
        }

        static float Square(float phase) => Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;
        static float Tri(float phase) { float p = Mathf.Repeat(phase, 1f); return p < 0.5f ? p * 4f - 1f : 3f - p * 4f; }

        /// 톤 하나를 버퍼에 더한다 (감쇠 지수 포함)
        static void Tone(float[] buf, float start, float dur, float freq, float amp, int wave, float decay = 6f)
        {
            int s = (int)(start * RATE), n = (int)(dur * RATE);
            float phase = 0;
            for (int i = 0; i < n && s + i < buf.Length; i++)
            {
                float t = i / (float)RATE;
                phase += freq / RATE;
                float env = Mathf.Exp(-decay * t / dur);
                float v = wave == 0 ? Mathf.Sin(phase * Mathf.PI * 2f) : wave == 1 ? Square(phase) : Tri(phase);
                buf[s + i] += v * amp * env;
            }
        }

        static void Noise(float[] buf, float start, float dur, float amp, float decay, int seed)
        {
            int s = (int)(start * RATE), n = (int)(dur * RATE);
            var rng = new System.Random(seed);
            for (int i = 0; i < n && s + i < buf.Length; i++)
            {
                float t = i / (float)RATE;
                buf[s + i] += (float)(rng.NextDouble() * 2 - 1) * amp * Mathf.Exp(-decay * t / dur);
            }
        }

        // ---- 효과음 정의 ----

        static AudioClip MakeClick()
        {
            var b = new float[(int)(0.07f * RATE)];
            Tone(b, 0f, 0.05f, 1100f, 0.22f, 2, 7f);
            Tone(b, 0.015f, 0.05f, 740f, 0.16f, 2, 7f);
            return Clip("click", b);
        }

        static AudioClip MakeCard()
        {
            var b = new float[(int)(0.1f * RATE)];
            Noise(b, 0f, 0.06f, 0.16f, 9f, 11);
            Tone(b, 0.02f, 0.06f, 620f, 0.14f, 2, 6f);
            return Clip("card", b);
        }

        static AudioClip MakeHit()
        {
            var b = new float[(int)(0.16f * RATE)];
            Noise(b, 0f, 0.09f, 0.32f, 8f, 7);
            Tone(b, 0f, 0.12f, 130f, 0.4f, 0, 8f);
            Tone(b, 0f, 0.05f, 70f, 0.3f, 1, 5f);
            return Clip("hit", b);
        }

        static AudioClip MakeGuard()
        {
            var b = new float[(int)(0.18f * RATE)];
            Tone(b, 0f, 0.14f, 880f, 0.2f, 0, 9f);
            Tone(b, 0f, 0.14f, 1320f, 0.12f, 0, 10f);
            Noise(b, 0f, 0.04f, 0.14f, 10f, 21);
            return Clip("guard", b);
        }

        static AudioClip MakeHeal()
        {
            var b = new float[(int)(0.3f * RATE)];
            Tone(b, 0f, 0.12f, 523f, 0.16f, 2, 4f);
            Tone(b, 0.09f, 0.12f, 659f, 0.16f, 2, 4f);
            Tone(b, 0.18f, 0.12f, 784f, 0.16f, 2, 4f);
            return Clip("heal", b);
        }

        static AudioClip MakeWin()
        {
            var b = new float[(int)(0.7f * RATE)];
            float[] notes = { 523f, 659f, 784f, 1046f };
            for (int i = 0; i < notes.Length; i++)
                Tone(b, i * 0.11f, 0.28f, notes[i], 0.17f, 2, 3.5f);
            return Clip("win", b);
        }

        static AudioClip MakeLose()
        {
            var b = new float[(int)(0.9f * RATE)];
            float[] notes = { 392f, 330f, 262f, 196f };
            for (int i = 0; i < notes.Length; i++)
                Tone(b, i * 0.16f, 0.34f, notes[i], 0.16f, 2, 3f);
            return Clip("lose", b);
        }

        // ---- BGM: 어둑한 던전 루프 (A단조, 8마디, 무한 루프) ----

        /// 순수 수학만 사용 — 워커 스레드에서 실행 가능
        static float[] MakeBgmSamples()
        {
            const float bpm = 92f;
            float beat = 60f / bpm;
            int bars = 8;
            float len = bars * 4 * beat;
            var b = new float[(int)(len * RATE)];

            // 코드 진행: Am - F - C - E (2마디씩) 근음
            float[] roots = { 110f, 87.31f, 65.41f, 82.41f }; // A2 F2 C2 E2
            float[][] arps =
            {
                new[] { 220f, 261.6f, 329.6f, 261.6f }, // A C E C
                new[] { 174.6f, 220f, 261.6f, 220f },   // F A C A
                new[] { 130.8f, 164.8f, 196f, 261.6f }, // C E G C
                new[] { 164.8f, 207.7f, 246.9f, 207.7f },// E G# B G#
            };

            for (int bar = 0; bar < bars; bar++)
            {
                int chord = (bar / 2) % 4;
                float barStart = bar * 4 * beat;
                // 저음 드론 — 마디 전체
                Tone(b, barStart, 4 * beat, roots[chord], 0.10f, 2, 0.8f);
                // 아르페지오 — 8분음표
                for (int i = 0; i < 8; i++)
                    Tone(b, barStart + i * beat * 0.5f, beat * 0.45f, arps[chord][i % 4] * (i >= 4 ? 2f : 1f), 0.055f, 2, 3f);
                // 박자 노이즈 햇 — 2·4박
                Noise(b, barStart + beat, 0.03f, 0.05f, 8f, bar * 31 + 3);
                Noise(b, barStart + 3 * beat, 0.03f, 0.05f, 8f, bar * 31 + 17);
            }

            // 루프 이음새 클릭 방지 — 앞뒤 30ms 페이드
            int fade = (int)(0.03f * RATE);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                b[i] *= k;
                b[b.Length - 1 - i] *= k;
            }
            return b;
        }
    }

    /// AudioKit의 메인 스레드 훅 — 워커가 합성한 BGM 버퍼를 회수해 클립으로 만든다
    class AudioKitHost : MonoBehaviour
    {
        void Update() => AudioKit.PollBgm();
    }
}
