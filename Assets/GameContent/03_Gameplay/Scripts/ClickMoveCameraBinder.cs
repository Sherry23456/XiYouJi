using UnityEngine;

namespace XiYouJi.Gameplay
{
    /// <summary>Lets a playable prefab reuse the map's camera and follow bounds.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PointClickNavController))]
    [DefaultExecutionOrder(50)]
    public sealed class ClickMoveCameraBinder : MonoBehaviour
    {
        [Tooltip("留空时使用地图上带 MainCamera 标签的相机。")]
        public Camera sceneCamera;
        public bool bindOnEnable = true;
        [Tooltip("为兼容旧预制体保留；绑定时会优先保留场景中已经摆好的相机位姿。")]
        public Vector3 fallbackOffset = new Vector3(-20f, 14f, 0f);

        private PointClickNavController movement;
        private BoundedDiscoCamera follow;
        private CameraOcclusionCuller occlusion;
        private Transform previousTarget;
        private Transform previousOcclusionTarget;
        private Vector3 previousOffset;
        private Camera previousInputCamera;
        private bool previousFollowEnabled;
        private bool addedFollow;
        private bool bound;

        private void Start()
        {
            movement = GetComponent<PointClickNavController>();
            if (bindOnEnable) BindCamera();
        }

        private void Update()
        {
            if (bindOnEnable && (!bound || sceneCamera == null)) BindCamera();
        }

        public void BindCamera()
        {
            if (bound && sceneCamera != null) return;
            if (movement == null) movement = GetComponent<PointClickNavController>();
            if (sceneCamera == null) sceneCamera = movement.inputCamera != null ? movement.inputCamera : Camera.main;
            if (sceneCamera == null) return;
            previousInputCamera = movement.inputCamera;
            movement.inputCamera = sceneCamera;
            follow = sceneCamera.GetComponent<BoundedDiscoCamera>();
            addedFollow = follow == null;
            if (addedFollow) follow = sceneCamera.gameObject.AddComponent<BoundedDiscoCamera>();
            previousTarget = follow.target;
            previousFollowEnabled = follow.enabled;
            previousOffset = previousTarget != null
                ? sceneCamera.transform.position - previousTarget.position
                : Vector3.zero;

            // Keep the camera pose authored in the scene. Configure captures a new
            // target-relative offset from the current pose, so binding a playable
            // character never teleports or rotates the camera on the first frame.
            follow.Configure(transform, follow.movementBounds);
            follow.enabled = true;
            occlusion = sceneCamera.GetComponent<CameraOcclusionCuller>();
            if (occlusion != null)
            {
                previousOcclusionTarget = occlusion.target;
                occlusion.target = transform;
            }
            bound = true;
        }

        private void OnDisable()
        {
            if (bound && follow != null && follow.target == transform)
            {
                if (previousTarget != null)
                    follow.transform.position = previousTarget.position + previousOffset;
                follow.Configure(previousTarget, follow.movementBounds);
                follow.enabled = previousFollowEnabled;
                if (addedFollow) Destroy(follow);
            }
            if (occlusion != null && occlusion.target == transform)
                occlusion.target = previousOcclusionTarget;
            if (bound && movement != null && movement.inputCamera == sceneCamera)
                movement.inputCamera = previousInputCamera;
            bound = false;
        }
    }
}
