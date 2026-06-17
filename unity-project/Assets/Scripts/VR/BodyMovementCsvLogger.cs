using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

namespace Aetheria.VR
{
    /// <summary>
    /// Writes head + hand poses to CSV for model training / session analysis.
    /// Attach this to XR Origin (or any object) and assign head/hand transforms if available.
    /// </summary>
    public sealed class BodyMovementCsvLogger : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private bool recordOnStart = true;
        [SerializeField] private float sampleHz = 20f;
        [SerializeField] private string filePrefix = "aetheria_body_pose";

        private bool _recording;
        private float _nextSampleTime;
        private string _filePath;
        private StreamWriter _writer;

        private void Awake()
        {
            if (head == null && Camera.main != null)
                head = Camera.main.transform;
            ResolveHandAnchors();
        }

        private void Start()
        {
            if (head == null && Camera.main != null)
                head = Camera.main.transform;

            if (leftHand == null || rightHand == null)
                ResolveHandAnchors();

            if (recordOnStart)
                StartRecording();
        }

        private void Update()
        {
            if (!_recording || Time.time < _nextSampleTime)
                return;

            _nextSampleTime = Time.time + 1f / Mathf.Max(1f, sampleHz);
            WriteSample();
        }

        private void OnDestroy()
        {
            StopRecording();
        }

        [ContextMenu("Start Recording")]
        public void StartRecording()
        {
            if (_recording)
                return;

            string fileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            _filePath = Path.Combine(Application.persistentDataPath, fileName);
            Directory.CreateDirectory(Application.persistentDataPath);
            _writer = new StreamWriter(_filePath, false, Encoding.UTF8);
            _writer.WriteLine("time,head_x,head_y,head_z,head_qx,head_qy,head_qz,head_qw,left_x,left_y,left_z,left_qx,left_qy,left_qz,left_qw,right_x,right_y,right_z,right_qx,right_qy,right_qz,right_qw");
            _recording = true;
            _nextSampleTime = Time.time;
            Debug.Log($"[BodyMovementCsvLogger] Recording started: {_filePath}");
        }

        [ContextMenu("Stop Recording")]
        public void StopRecording()
        {
            if (!_recording)
                return;
            _recording = false;
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
            Debug.Log($"[BodyMovementCsvLogger] Recording saved: {_filePath}");
        }

        private void WriteSample()
        {
            Pose h = GetPose(head);
            Pose l = GetPose(leftHand);
            Pose r = GetPose(rightHand);
            _writer?.WriteLine(
                $"{Time.time:0.000}," +
                $"{h.position.x:0.####},{h.position.y:0.####},{h.position.z:0.####},{h.rotation.x:0.####},{h.rotation.y:0.####},{h.rotation.z:0.####},{h.rotation.w:0.####}," +
                $"{l.position.x:0.####},{l.position.y:0.####},{l.position.z:0.####},{l.rotation.x:0.####},{l.rotation.y:0.####},{l.rotation.z:0.####},{l.rotation.w:0.####}," +
                $"{r.position.x:0.####},{r.position.y:0.####},{r.position.z:0.####},{r.rotation.x:0.####},{r.rotation.y:0.####},{r.rotation.z:0.####},{r.rotation.w:0.####}");
            _writer?.Flush();
        }

        private static Pose GetPose(Transform t)
        {
            if (t == null)
                return new Pose(Vector3.zero, Quaternion.identity);
            return new Pose(t.position, t.rotation);
        }

        private void ResolveHandAnchors()
        {
            Transform root = transform;
            var xr = GameObject.Find("XR Origin (XR Rig)");
            if (xr != null)
                root = xr.transform;
            if (leftHand == null)
            {
                var left = XRHandAttachmentUtil.FindHandController(root, false);
                leftHand = XRHandAttachmentUtil.FindIndexTipOrController(left);
            }
            if (rightHand == null)
            {
                var right = XRHandAttachmentUtil.FindHandController(root, true);
                rightHand = XRHandAttachmentUtil.FindIndexTipOrController(right);
            }
        }
    }
}
