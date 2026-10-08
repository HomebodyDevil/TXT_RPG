using UnityEngine;

namespace TxTRPG.UI.Dice
{
    // Root of DiceStage.prefab. Holds the Prefab-internal references the panel needs and applies a computed arena
    // to the camera and visible floor. The Models root must keep an identity local rotation and zero local position.
    [DisallowMultipleComponent]
    public sealed class DiceStageRig : MonoBehaviour
    {
        [SerializeField] private Camera stageCamera;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Transform floor;

        public Camera StageCamera => stageCamera;
        public Transform ModelRoot => modelRoot;
        public Transform Floor => floor;
        public bool IsComplete => stageCamera != null && modelRoot != null && floor != null;

        public void ApplyComposition(DiceArena arena, float cameraDistance)
        {
            if (!IsComplete || !arena.IsValid) return;
            var rotation = Quaternion.Euler(arena.Pitch, 0f, 0f);
            var cameraTransform = stageCamera.transform;
            cameraTransform.localRotation = rotation;
            cameraTransform.localPosition = modelRoot.localPosition - rotation * Vector3.forward * cameraDistance;
            stageCamera.orthographic = true;
            stageCamera.orthographicSize = arena.OrthographicSize;
            stageCamera.nearClipPlane = 0.1f;
            stageCamera.farClipPlane = cameraDistance * 2f + arena.Depth;
            floor.localPosition = modelRoot.localPosition + new Vector3(0f, -0.01f, arena.CenterZ);
            floor.localRotation = Quaternion.Euler(90f, 0f, 0f);
            floor.localScale = new Vector3(arena.HalfWidth * 2f, arena.Depth, 1f);
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(Camera camera, Transform models, Transform visibleFloor)
        { stageCamera = camera; modelRoot = models; floor = visibleFloor; }
#endif
    }
}
