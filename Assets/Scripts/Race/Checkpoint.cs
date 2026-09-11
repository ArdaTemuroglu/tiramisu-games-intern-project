using UnityEngine;

namespace ArcadeRacing
{
    [RequireComponent(typeof(Collider))]
    public sealed class Checkpoint : MonoBehaviour
    {
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private GameObject activeVisual;

        private CheckpointManager checkpointManager;
        private bool isStartFinish;

        public int Index { get; private set; } = -1;
        public bool IsStartFinish => isStartFinish;
        public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;
        public Quaternion RespawnRotation => respawnPoint != null ? respawnPoint.rotation : transform.rotation;

        private void Reset()
        {
            ConfigureColliders();
        }

        private void Awake()
        {
            ConfigureColliders();
        }

        private void OnValidate()
        {
            ConfigureColliders();
        }

        private void OnTriggerEnter(Collider other)
        {
            NotifyManager(other);
        }

        private void OnTriggerStay(Collider other)
        {
            NotifyManager(other);
        }

        public void Initialize(CheckpointManager manager, int index, bool startFinish)
        {
            checkpointManager = manager;
            Index = index;
            isStartFinish = startFinish;
        }

        public void SetExpected(bool isExpected)
        {
            if (activeVisual == null)
            {
                return;
            }

            if (activeVisual == gameObject || transform.IsChildOf(activeVisual.transform))
            {
                Debug.LogError(
                    $"{name} Active Visual alanına checkpoint nesnesinin kendisi veya üst nesnesi atanamaz.",
                    this
                );
                return;
            }

            activeVisual.SetActive(isExpected);
        }

        private void NotifyManager(Collider other)
        {
            if (checkpointManager == null || other == null)
            {
                return;
            }

            CarController carController = null;

            if (other.attachedRigidbody != null)
            {
                carController = other.attachedRigidbody.GetComponentInParent<CarController>();
            }

            if (carController == null)
            {
                carController = other.GetComponentInParent<CarController>();
            }

            if (carController != null)
            {
                checkpointManager.TryPassCheckpoint(this, carController);
            }
        }

        private void ConfigureColliders()
        {
            Collider[] checkpointColliders = GetComponents<Collider>();

            for (int i = 0; i < checkpointColliders.Length; i++)
            {
                checkpointColliders[i].isTrigger = true;
            }
        }
    }
}