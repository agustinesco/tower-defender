using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Entities;

namespace TowerDefense.Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const int AudioSourcePoolSize = 8;
        private Queue<AudioSource> audioSourcePool = new Queue<AudioSource>();

        // Procedural clips
        private AudioClip clipArrowShoot;
        private AudioClip clipCannonShoot;
        private AudioClip clipShotgunShoot;
        private AudioClip clipTeslaShoot;
        private AudioClip clipFlameShoot;
        private AudioClip clipEnemyDeath;
        private AudioClip clipImpact;
        private AudioClip clipCastleHit;
        private AudioClip clipWaveStart;
        private AudioClip clipWaveComplete;
        private AudioClip clipAmbientLoop;

        private AudioSource ambientSource;

        [SerializeField] private float masterVolume = 0.5f;
        [SerializeField] private float sfxVolume = 0.7f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Ensure there's an AudioListener somewhere in the scene
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                if (cam != null)
                    cam.gameObject.AddComponent<AudioListener>();
                else
                    gameObject.AddComponent<AudioListener>();
            }

            CreateAudioSourcePool();
            GenerateAllClips();

            // Dedicated ambient source
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.volume = masterVolume * 0.15f;
            ambientSource.playOnAwake = false;
        }

        private void CreateAudioSourcePool()
        {
            for (int i = 0; i < AudioSourcePoolSize; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                audioSourcePool.Enqueue(source);
            }
        }

        private AudioSource GetSource()
        {
            // Find an available source (not playing)
            int count = audioSourcePool.Count;
            for (int i = 0; i < count; i++)
            {
                var source = audioSourcePool.Dequeue();
                audioSourcePool.Enqueue(source);
                if (!source.isPlaying)
                    return source;
            }
            // All busy, reuse oldest but keep it in the pool
            var oldest = audioSourcePool.Dequeue();
            audioSourcePool.Enqueue(oldest);
            return oldest;
        }

        private void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var source = GetSource();
            source.clip = clip;
            source.volume = masterVolume * sfxVolume * volume;
            source.pitch = pitch + Random.Range(-0.05f, 0.05f);
            source.spatialBlend = 0f;
            source.Play();
        }

        // --- Public API ---

        public void PlayTowerShoot(TowerData data, Vector3 pos)
        {
            if (data == null) return;

            AudioClip clip;
            if (data.isTesla) clip = clipTeslaShoot;
            else if (data.isShotgun) clip = clipShotgunShoot;
            else if (data.isFlame) clip = clipFlameShoot;
            else if (data.isAreaDamage) clip = clipCannonShoot;
            else clip = clipArrowShoot;

            PlayClip(clip, 0.6f);
        }

        public void PlayEnemyDeath(Vector3 pos)
        {
            PlayClip(clipEnemyDeath, 0.5f);
        }

        public void PlayImpact(Vector3 pos)
        {
            PlayClip(clipImpact, 0.3f);
        }

        public void PlayCastleHit()
        {
            PlayClip(clipCastleHit, 0.8f);
        }

        public void PlayWaveStart()
        {
            PlayClip(clipWaveStart, 0.7f);
        }

        public void PlayWaveComplete()
        {
            PlayClip(clipWaveComplete, 0.7f);
        }

        public void StartAmbient()
        {
            if (ambientSource != null && clipAmbientLoop != null && !ambientSource.isPlaying)
            {
                ambientSource.clip = clipAmbientLoop;
                ambientSource.Play();
            }
        }

        public void StopAmbient()
        {
            if (ambientSource != null)
                ambientSource.Stop();
        }

        // --- Procedural Clip Generation ---

        private void GenerateAllClips()
        {
            clipArrowShoot = GenerateArrowShoot();
            clipCannonShoot = GenerateCannonShoot();
            clipShotgunShoot = GenerateShotgunShoot();
            clipTeslaShoot = GenerateTeslaShoot();
            clipFlameShoot = GenerateFlameShoot();
            clipEnemyDeath = GenerateEnemyDeath();
            clipImpact = GenerateImpact();
            clipCastleHit = GenerateCastleHit();
            clipWaveStart = GenerateWaveStart();
            clipWaveComplete = GenerateWaveComplete();
            clipAmbientLoop = GenerateAmbientLoop();
        }

        private static AudioClip CreateClip(string name, float duration, System.Func<int, int, float> generator)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                data[i] = generator(i, sampleRate);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateArrowShoot()
        {
            return CreateClip("ArrowShoot", 0.08f, (i, sr) =>
            {
                float t = (float)i / sr;
                float envelope = 1f - t / 0.08f;
                return Mathf.Sin(2f * Mathf.PI * 220f * t) * envelope * 0.5f;
            });
        }

        private AudioClip GenerateCannonShoot()
        {
            return CreateClip("CannonShoot", 0.15f, (i, sr) =>
            {
                float t = (float)i / sr;
                float envelope = 1f - t / 0.15f;
                float sine = Mathf.Sin(2f * Mathf.PI * 80f * t);
                float noise = (Random.value * 2f - 1f) * Mathf.Max(0f, 1f - t / 0.05f);
                return (sine * 0.6f + noise * 0.4f) * envelope * 0.6f;
            });
        }

        private AudioClip GenerateShotgunShoot()
        {
            return CreateClip("ShotgunShoot", 0.1f, (i, sr) =>
            {
                float t = (float)i / sr;
                float envelope = 1f - t / 0.1f;
                float noise = Random.value * 2f - 1f;
                return noise * envelope * envelope * 0.5f;
            });
        }

        private AudioClip GenerateTeslaShoot()
        {
            return CreateClip("TeslaShoot", 0.12f, (i, sr) =>
            {
                float t = (float)i / sr;
                float envelope = 1f - t / 0.12f;
                float freq = Mathf.Lerp(400f, 2000f, t / 0.12f);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.4f;
            });
        }

        private AudioClip GenerateFlameShoot()
        {
            return CreateClip("FlameShoot", 0.2f, (i, sr) =>
            {
                float t = (float)i / sr;
                float fadeIn = Mathf.Min(t / 0.05f, 1f);
                float fadeOut = 1f - Mathf.Max(0f, (t - 0.1f) / 0.1f);
                float noise = Random.value * 2f - 1f;
                return noise * fadeIn * fadeOut * 0.4f;
            });
        }

        private AudioClip GenerateEnemyDeath()
        {
            return CreateClip("EnemyDeath", 0.15f, (i, sr) =>
            {
                float t = (float)i / sr;
                float envelope = 1f - t / 0.15f;
                float freq = Mathf.Lerp(600f, 200f, t / 0.15f);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.5f;
            });
        }

        private AudioClip GenerateImpact()
        {
            return CreateClip("Impact", 0.05f, (i, sr) =>
            {
                float t = (float)i / sr;
                float envelope = 1f - t / 0.05f;
                float noise = Random.value * 2f - 1f;
                return noise * envelope * envelope * 0.4f;
            });
        }

        private AudioClip GenerateCastleHit()
        {
            return CreateClip("CastleHit", 0.3f, (i, sr) =>
            {
                float t = (float)i / sr;
                float envelope = 1f - t / 0.3f;
                float sine = Mathf.Sin(2f * Mathf.PI * 60f * t);
                float noise = (Random.value * 2f - 1f) * Mathf.Max(0f, 1f - t / 0.1f);
                return (sine * 0.5f + noise * 0.3f) * envelope * 0.7f;
            });
        }

        private AudioClip GenerateWaveStart()
        {
            return CreateClip("WaveStart", 0.4f, (i, sr) =>
            {
                float t = (float)i / sr;
                float envelope = Mathf.Min(t / 0.05f, 1f) * (1f - Mathf.Max(0f, (t - 0.3f) / 0.1f));
                // 3-note ascending arpeggio: C5, E5, G5
                float freq;
                if (t < 0.13f) freq = 523f;
                else if (t < 0.26f) freq = 659f;
                else freq = 784f;
                return Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.4f;
            });
        }

        private AudioClip GenerateWaveComplete()
        {
            return CreateClip("WaveComplete", 0.5f, (i, sr) =>
            {
                float t = (float)i / sr;
                float envelope = Mathf.Min(t / 0.05f, 1f) * (1f - Mathf.Max(0f, (t - 0.35f) / 0.15f));
                // Major chord swell: C + E + G
                float c = Mathf.Sin(2f * Mathf.PI * 523f * t);
                float e = Mathf.Sin(2f * Mathf.PI * 659f * t);
                float g = Mathf.Sin(2f * Mathf.PI * 784f * t);
                return (c + e + g) / 3f * envelope * 0.4f;
            });
        }

        private AudioClip GenerateAmbientLoop()
        {
            return CreateClip("AmbientLoop", 8f, (i, sr) =>
            {
                float t = (float)i / sr;
                // Slow modulated sine drone
                float mod = Mathf.Sin(2f * Mathf.PI * 0.2f * t);
                float freq = 80f + mod * 20f;
                return Mathf.Sin(2f * Mathf.PI * freq * t) * 0.15f;
            });
        }
    }
}
