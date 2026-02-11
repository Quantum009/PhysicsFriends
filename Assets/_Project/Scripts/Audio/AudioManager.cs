// ============================================================
// AudioManager.cs — 音频管理器：BGM + SFX + 时代切换淡入淡出
// ============================================================
using UnityEngine;
using System.Collections;
using PhysicsFriends.Core;
using PhysicsFriends.Utils;

namespace PhysicsFriends.Audio
{
    public class AudioManager : Singleton<AudioManager>
    {
        [Header("音源")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("BGM")]
        [SerializeField] private AudioClip bgm_NaturalPhilosophy;
        [SerializeField] private AudioClip bgm_ClassicalPhysics;
        [SerializeField] private AudioClip bgm_ModernPhysics;
        [SerializeField] private AudioClip bgm_MainMenu;

        [Header("SFX")]
        [SerializeField] private AudioClip sfx_DiceRoll;
        [SerializeField] private AudioClip sfx_PawnMove;
        [SerializeField] private AudioClip sfx_CardDraw;
        [SerializeField] private AudioClip sfx_Synthesis;
        [SerializeField] private AudioClip sfx_MolGain;
        [SerializeField] private AudioClip sfx_MolLose;
        [SerializeField] private AudioClip sfx_Achievement;
        [SerializeField] private AudioClip sfx_Victory;
        [SerializeField] private AudioClip sfx_EventBad;
        [SerializeField] private AudioClip sfx_EventGood;
        [SerializeField] private AudioClip sfx_ButtonClick;

        [Header("设置")]
        [SerializeField] private float crossFadeDuration = 1.5f;
        [SerializeField] [Range(0f, 1f)] private float bgmVolume = 0.6f;
        [SerializeField] [Range(0f, 1f)] private float sfxVolume = 0.8f;

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);

            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
            }
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
            }
        }

        private void OnEnable()
        {
            GameEvents.OnEraChanged += OnEraChanged;
            GameEvents.OnDiceRolled += (_, _) => PlaySFX(sfx_DiceRoll);
            GameEvents.OnSynthesisCompleted += (_, _) => PlaySFX(sfx_Synthesis);
            GameEvents.OnMolChanged += OnMolChanged;
            GameEvents.OnAchievementCompleted += (_, _) => PlaySFX(sfx_Achievement);
            GameEvents.OnVictory += (_, _) => PlaySFX(sfx_Victory);
        }

        private void OnDisable()
        {
            GameEvents.OnEraChanged -= OnEraChanged;
        }

        // ================================================================
        // 公开接口
        // ================================================================

        public void PlaySFX(AudioClip clip)
        {
            if (clip != null && sfxSource != null)
                sfxSource.PlayOneShot(clip, sfxVolume);
        }

        public void PlayButtonClick() => PlaySFX(sfx_ButtonClick);

        public void PlayBGM(AudioClip clip)
        {
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;
            StartCoroutine(CrossFadeBGM(clip));
        }

        public void StopBGM()
        {
            StartCoroutine(FadeOutBGM());
        }

        public void PlayMenuBGM() => PlayBGM(bgm_MainMenu);

        public void SetBGMVolume(float vol)
        {
            bgmVolume = Mathf.Clamp01(vol);
            bgmSource.volume = bgmVolume;
        }

        public void SetSFXVolume(float vol)
        {
            sfxVolume = Mathf.Clamp01(vol);
        }

        // ================================================================
        // 内部逻辑
        // ================================================================

        private void OnEraChanged(Era era)
        {
            var clip = era switch
            {
                Era.NaturalPhilosophy => bgm_NaturalPhilosophy,
                Era.ClassicalPhysics => bgm_ClassicalPhysics,
                Era.ModernPhysics => bgm_ModernPhysics,
                _ => bgm_NaturalPhilosophy
            };
            PlayBGM(clip);
        }

        private void OnMolChanged(int idx, int oldVal, int newVal)
        {
            PlaySFX(newVal > oldVal ? sfx_MolGain : sfx_MolLose);
        }

        private IEnumerator CrossFadeBGM(AudioClip newClip)
        {
            float half = crossFadeDuration / 2f;

            // 淡出
            if (bgmSource.isPlaying)
            {
                float start = bgmSource.volume;
                float t = 0;
                while (t < half)
                {
                    t += Time.deltaTime;
                    bgmSource.volume = Mathf.Lerp(start, 0f, t / half);
                    yield return null;
                }
                bgmSource.Stop();
            }

            // 切换并淡入
            if (newClip != null)
            {
                bgmSource.clip = newClip;
                bgmSource.volume = 0f;
                bgmSource.Play();

                float t = 0;
                while (t < half)
                {
                    t += Time.deltaTime;
                    bgmSource.volume = Mathf.Lerp(0f, bgmVolume, t / half);
                    yield return null;
                }
                bgmSource.volume = bgmVolume;
            }
        }

        private IEnumerator FadeOutBGM()
        {
            float start = bgmSource.volume;
            float t = 0;
            while (t < crossFadeDuration)
            {
                t += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(start, 0f, t / crossFadeDuration);
                yield return null;
            }
            bgmSource.Stop();
        }
    }
}
