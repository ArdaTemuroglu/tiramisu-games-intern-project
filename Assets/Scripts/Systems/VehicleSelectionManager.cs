using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcadeRacing
{
    [Serializable]
    public sealed class VehicleOption
    {
        public string displayName = "Vehicle";
        public GameObject vehicleRoot;
        public CarController carController;
        public NitroSystem nitroSystem;

        public void ResolveReferences()
        {
            if (vehicleRoot == null && carController != null)
            {
                vehicleRoot = carController.gameObject;
            }

            if (vehicleRoot == null)
            {
                return;
            }

            if (carController == null)
            {
                carController =
                    vehicleRoot.GetComponentInChildren<CarController>(true);
            }

            if (nitroSystem == null)
            {
                nitroSystem =
                    vehicleRoot.GetComponentInChildren<NitroSystem>(true);
            }
        }
    }

    [DefaultExecutionOrder(-500)]
    public sealed class VehicleSelectionManager : MonoBehaviour
    {
        [Header("Vehicles")]
        [SerializeField] private List<VehicleOption> vehicles =
            new List<VehicleOption>();

        [Header("Systems")]
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private UpgradeManager upgradeManager;

        public int SelectedVehicleIndex { get; private set; } = -1;
        public bool HasSelection => SelectedVehicleIndex >= 0;
        public int VehicleCount => vehicles.Count;

        private void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            ResolveAllReferences();
            DeactivateAllVehicles();
            ClearRuntimeBindings();
        }

        private void Start()
        {
            ValidateReferences();
            uiManager.ShowVehicleSelection();
        }

        private void OnValidate()
        {
            ResolveAllReferences();
        }

        public void SelectVehicle1()
        {
            SelectVehicle(0);
        }

        public void SelectVehicle2()
        {
            SelectVehicle(1);
        }

        public void SelectVehicle(int index)
        {
            if (index < 0 || index >= vehicles.Count)
            {
                Debug.LogError(
                    $"Vehicle index {index} is outside the configured vehicle list.",
                    this
                );
                return;
            }

            VehicleOption selectedOption = vehicles[index];
            selectedOption.ResolveReferences();

            if (
                selectedOption.vehicleRoot == null ||
                selectedOption.carController == null ||
                selectedOption.nitroSystem == null
            )
            {
                Debug.LogError(
                    $"Vehicle option {index + 1} is missing its root, CarController or NitroSystem reference.",
                    this
                );
                return;
            }

            Time.timeScale = 1f;
            AudioListener.pause = false;

            DeactivateAllVehicles();
            selectedOption.vehicleRoot.SetActive(true);
            selectedOption.carController.SetControlEnabled(false);

            SelectedVehicleIndex = index;

            string vehicleName = string.IsNullOrWhiteSpace(
                selectedOption.displayName
            )
                ? $"Vehicle {index + 1}"
                : selectedOption.displayName.Trim();

            raceManager.SetPlayerCar(
                selectedOption.carController,
                vehicleName
            );

            cameraFollow.SetTarget(selectedOption.carController, true);

            uiManager.BindVehicle(
                selectedOption.carController,
                selectedOption.nitroSystem
            );

            upgradeManager.BindVehicle(
                selectedOption.carController,
                selectedOption.nitroSystem
            );

            uiManager.SetSelectedVehicleName(vehicleName);
            uiManager.HideVehicleSelection();
            raceManager.RequestRaceStart();
        }

        private void ResolveAllReferences()
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                vehicles[i]?.ResolveReferences();
            }
        }

        private void DeactivateAllVehicles()
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                VehicleOption option = vehicles[i];

                if (option == null || option.vehicleRoot == null)
                {
                    continue;
                }

                if (option.vehicleRoot == gameObject)
                {
                    Debug.LogError(
                        "VehicleSelectionManager cannot use its own GameObject as a vehicle root.",
                        this
                    );
                    continue;
                }

                option.vehicleRoot.SetActive(false);
            }
        }

        private void ClearRuntimeBindings()
        {
            SelectedVehicleIndex = -1;

            if (raceManager != null)
            {
                raceManager.SetPlayerCar(null);
            }

            if (cameraFollow != null)
            {
                cameraFollow.ClearTarget();
            }

            if (uiManager != null)
            {
                uiManager.BindVehicle(null, null);
            }

            if (upgradeManager != null)
            {
                upgradeManager.BindVehicle(null, null);
            }
        }

        private void ValidateReferences()
        {
            if (
                raceManager == null ||
                cameraFollow == null ||
                uiManager == null ||
                upgradeManager == null
            )
            {
                throw new MissingReferenceException(
                    "VehicleSelectionManager requires RaceManager, CameraFollow, UIManager and UpgradeManager references."
                );
            }

            if (vehicles.Count < 2)
            {
                throw new MissingReferenceException(
                    "VehicleSelectionManager requires at least two vehicle options."
                );
            }

            for (int i = 0; i < vehicles.Count; i++)
            {
                VehicleOption option = vehicles[i];
                option?.ResolveReferences();

                if (
                    option == null ||
                    option.vehicleRoot == null ||
                    option.carController == null ||
                    option.nitroSystem == null
                )
                {
                    throw new MissingReferenceException(
                        $"Vehicle option {i + 1} is incomplete."
                    );
                }
            }
        }
    }
}
