using System;
using UnityEngine;

namespace DrumKit.Rhythm
{
    /// <summary>
    /// Single source of truth for "where are we in the song". Schedules playback via
    /// AudioSettings.dspTime (the audio hardware's own clock) rather than Time.time, which
    /// is integrated per-frame and drifts against the audio clock over a multi-minute
    /// track. Every other rhythm component (cue rings, scorer) must read SongPositionSeconds
    /// here rather than computing its own timer, so nothing can drift independently.
    ///
    /// Position is tracked as a (dspTime, songPosition) baseline pair rebased on every
    /// pause/resume/seek/speed-change, rather than a single fixed "start time" - this keeps
    /// SongPositionSeconds correct (in terms of the song's own, un-slowed timeline) even
    /// while played back at a different AudioSource.pitch, e.g. slowed down 0.5x/0.3x to
    /// make hits easier to place precisely while authoring a chart.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Conductor : MonoBehaviour
    {
        [SerializeField] SongChart chart;
        [Tooltip("How far in the future (seconds) playback is scheduled - just needs to be comfortably positive so PlayScheduled isn't scheduled in the past.")]
        [SerializeField] float leadInSeconds = 1f;
        [Tooltip("Start the song as soon as the scene loads. Turn off if a menu/countdown should call StartSong() instead.")]
        [SerializeField] bool autoStart = true;

        AudioSource m_MusicSource;
        double m_BaselineDspTime;
        float m_BaselineSongPosition;
        float m_PlaybackSpeed = 1f;
        bool m_Started;

        public SongChart Chart => chart;
        public bool IsStarted => m_Started;
        public bool IsPlayingClip => m_MusicSource.isPlaying;
        public bool IsPaused { get; private set; }
        public float PlaybackSpeed => m_PlaybackSpeed;

        /// <summary>
        /// Seconds elapsed since the audio clip actually started, in the song's own
        /// (un-slowed) timeline - negative during the lead-in, frozen while IsPaused, and
        /// unaffected by PlaybackSpeed (a note recorded while slowed down still lands at
        /// its true position in the original track).
        /// </summary>
        public float SongPositionSeconds { get; private set; }

        void Awake()
        {
            m_MusicSource = GetComponent<AudioSource>();
            m_MusicSource.playOnAwake = false;
        }

        void Start()
        {
            if (autoStart)
            {
                StartSong();
            }
        }

        public void StartSong()
        {
            if (chart == null || chart.song == null)
            {
                Debug.LogError($"{nameof(Conductor)} on '{name}' has no chart/song assigned.", this);
                return;
            }

            m_MusicSource.clip = chart.song;
            m_MusicSource.pitch = m_PlaybackSpeed;
            double scheduledStartDspTime = AudioSettings.dspTime + leadInSeconds;
            m_MusicSource.PlayScheduled(scheduledStartDspTime);

            m_BaselineDspTime = AudioSettings.dspTime;
            m_BaselineSongPosition = -leadInSeconds;
            SongPositionSeconds = m_BaselineSongPosition;
            m_Started = true;
        }

        void Update()
        {
            if (!m_Started || IsPaused)
            {
                return;
            }

            SongPositionSeconds = m_BaselineSongPosition + (float)(AudioSettings.dspTime - m_BaselineDspTime) * m_PlaybackSpeed;
        }

        /// <summary>Snapshots the current position as the new baseline - call before changing anything (speed/pause/seek) that would otherwise make the running calculation jump.</summary>
        void Rebase(float songPosition)
        {
            m_BaselineDspTime = AudioSettings.dspTime;
            m_BaselineSongPosition = songPosition;
            SongPositionSeconds = songPosition;
        }

        /// <summary>Freezes SongPositionSeconds and the underlying AudioSource in place.</summary>
        public void Pause()
        {
            if (!m_Started || IsPaused)
            {
                return;
            }

            m_MusicSource.Pause();
            IsPaused = true;
        }

        /// <summary>Resumes exactly where Pause() froze.</summary>
        public void Resume()
        {
            if (!m_Started || !IsPaused)
            {
                return;
            }

            Rebase(SongPositionSeconds);
            m_MusicSource.UnPause();
            IsPaused = false;
        }

        /// <summary>Jumps playback to an absolute song position (seconds), e.g. to rewind and redo a section while authoring a chart.</summary>
        public void SeekTo(float seconds)
        {
            if (!m_Started)
            {
                return;
            }

            seconds = Mathf.Clamp(seconds, 0f, m_MusicSource.clip.length - 0.01f);
            m_MusicSource.time = seconds;
            Rebase(seconds);
        }

        public void SeekRelative(float deltaSeconds) => SeekTo(SongPositionSeconds + deltaSeconds);

        /// <summary>
        /// Changes AudioSource.pitch (and thus playback rate) without disturbing
        /// SongPositionSeconds - handy for slowing a reference track down (0.5x, 0.3x...)
        /// to place hits more precisely while authoring a chart. Note: Unity's pitch shift
        /// also drops the audio's tone lower as it slows (no separate time-stretch), which
        /// doesn't matter for judging rhythm but is worth expecting.
        /// </summary>
        public void SetPlaybackSpeed(float speed)
        {
            speed = Mathf.Max(0.05f, speed);
            if (m_Started)
            {
                Rebase(SongPositionSeconds);
                m_MusicSource.pitch = speed;
            }

            m_PlaybackSpeed = speed;
        }
    }
}
