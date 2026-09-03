using System;
using System.Collections.Generic;
using JuegoCriminal.Player;
using JuegoCriminal.Printing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JuegoCriminal.Services
{
    /// <summary>
    /// Recopila el estado de los objetos pertenecientes a la escena actual antes
    /// de que SaveService serialice el slot. La UI solo solicita guardar.
    /// </summary>
    public sealed class WorldSaveService : MonoBehaviour
    {
        public bool CaptureCurrentWorld(SaveService save)
        {
            if (save == null || !save.HasCurrentGame) return false;

            CapturePlayers(save);
            CapturePrinters(save);
            save.SetLastScene(SceneManager.GetActiveScene().name);
            return true;
        }

        private static void CapturePlayers(SaveService save)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            Array.Sort(players, (a, b) => string.CompareOrdinal(a.name, b.name));

            int count = Mathf.Min(players.Length, SaveService.MaxPlayers);
            var states = new List<PlayerSaveState>(count);
            for (int i = 0; i < count; i++)
            {
                ThirdPersonController controller = players[i].GetComponent<ThirdPersonController>();
                states.Add(new PlayerSaveState(
                    players[i].transform.position,
                    players[i].transform.eulerAngles.y,
                    controller != null ? controller.LookPitch : 0f,
                    controller != null));
            }

            save.UpdatePlayerStates(states);
        }

        private static void CapturePrinters(SaveService save)
        {
            Printer3DController[] printers = FindObjectsByType<Printer3DController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var states = new List<PrinterSaveState>(printers.Length);
            for (int i = 0; i < printers.Length; i++)
                states.Add(printers[i].CaptureSaveState());

            save.UpdatePrinterStates(SceneManager.GetActiveScene().name, states);
        }
    }
}
