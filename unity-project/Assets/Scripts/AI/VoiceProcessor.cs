using System;
using UnityEngine;
using Aetheria.AI;
using Aetheria.Voice;

namespace Aetheria.AI
{
    /// <summary>
    /// Handles voice recording and sends to AI backend for transcription
    /// </summary>
    public class VoiceProcessor : MonoBehaviour
    {
        [Header("Recording Settings")]
        [SerializeField] private int sampleRate = 44100;
        [SerializeField] private int maxRecordingLength = 10; // seconds

        private AudioClip recordingClip;
        private bool isRecording = false;
        private string microphoneDevice;

        public event System.Action<string> OnTranscriptionComplete;
        public event System.Action<string> OnTranscriptionError;

        private void Start()
        {
            // Get default microphone
            if (Microphone.devices.Length > 0)
            {
                microphoneDevice = Microphone.devices[0];
                Debug.Log($"Using microphone: {microphoneDevice}");
            }
            else
            {
                Debug.LogWarning("No microphone found!");
            }
        }

        /// <summary>
        /// Start recording voice
        /// </summary>
        public void StartRecording()
        {
            if (isRecording)
            {
                Debug.LogWarning("Already recording!");
                return;
            }

            if (string.IsNullOrEmpty(microphoneDevice))
            {
                OnTranscriptionError?.Invoke("No microphone available");
                return;
            }

            recordingClip = Microphone.Start(microphoneDevice, false, maxRecordingLength, sampleRate);
            isRecording = true;
            Debug.Log("Started recording...");
        }

        /// <summary>
        /// Stop recording and transcribe
        /// </summary>
        public void StopRecordingAndTranscribe()
        {
            if (!isRecording)
            {
                Debug.LogWarning("Not recording!");
                return;
            }

            Microphone.End(microphoneDevice);
            isRecording = false;

            // Convert AudioClip to WAV bytes
            byte[] audioData = AudioClipToWAV(recordingClip);
            
            // Send to AI backend for transcription
            if (AIClient.Instance != null)
            {
                AIClient.Instance.TranscribeVoice(
                    audioData,
                    "recording.wav",
                    (transcription) =>
                    {
                        Debug.Log($"Transcription: {transcription}");
                        OnTranscriptionComplete?.Invoke(transcription);
                        
                        // Also send to VoiceIntegrationSystem if available
                        var voiceSystem = FindObjectOfType<VoiceIntegrationSystem>();
                        if (voiceSystem != null)
                        {
                            voiceSystem.ProcessVoiceCommand(transcription);
                        }
                    },
                    (error) =>
                    {
                        Debug.LogError($"Transcription error: {error}");
                        OnTranscriptionError?.Invoke(error);
                    }
                );
            }
            else
            {
                Debug.LogWarning("AIClient not available, skipping transcription");
            }
        }

        /// <summary>
        /// Convert AudioClip to WAV format bytes
        /// </summary>
        private byte[] AudioClipToWAV(AudioClip clip)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            // Convert to 16-bit PCM
            byte[] pcmData = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = (short)(samples[i] * 32767f);
                pcmData[i * 2] = (byte)(sample & 0xFF);
                pcmData[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            // Create WAV header
            int headerSize = 44;
            byte[] wavData = new byte[headerSize + pcmData.Length];

            // WAV header
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wavData, 0);
            System.BitConverter.GetBytes(wavData.Length - 8).CopyTo(wavData, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wavData, 8);
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wavData, 12);
            System.BitConverter.GetBytes(16).CopyTo(wavData, 16); // fmt chunk size
            System.BitConverter.GetBytes((ushort)1).CopyTo(wavData, 20); // audio format (PCM)
            System.BitConverter.GetBytes((ushort)clip.channels).CopyTo(wavData, 22);
            System.BitConverter.GetBytes(clip.frequency).CopyTo(wavData, 24);
            System.BitConverter.GetBytes(clip.frequency * clip.channels * 2).CopyTo(wavData, 28); // byte rate
            System.BitConverter.GetBytes((ushort)(clip.channels * 2)).CopyTo(wavData, 32); // block align
            System.BitConverter.GetBytes((ushort)16).CopyTo(wavData, 34); // bits per sample
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wavData, 36);
            System.BitConverter.GetBytes(pcmData.Length).CopyTo(wavData, 40);

            // Copy PCM data
            pcmData.CopyTo(wavData, headerSize);

            return wavData;
        }

        public bool IsRecording => isRecording;
    }
}

