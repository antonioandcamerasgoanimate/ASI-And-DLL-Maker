using System;
using System.Windows.Forms;
using GTA;
using GTA.Math;

public class VehicleSpawnerScript : Script
{
    public VehicleSpawnerScript()
    {
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F10)
        {
            SpawnVehicle("adder");
        }
    }

    private void SpawnVehicle(string modelName)
    {
        var model = new Model(modelName);
        if (model.IsValid && model.IsInCdImage)
        {
            // Request and wait for model loading
            model.Request(1000);
            if (model.IsLoaded)
            {
                var playerPed = Game.Player.Character;
                
                // Spawn vehicle in front of player
                Vector3 position = playerPed.Position + playerPed.ForwardVector * 5.0f;
                float heading = playerPed.Heading;

                var vehicle = World.CreateVehicle(model, position, heading);
                if (vehicle != null)
                {
                    vehicle.PlaceOnGround();
                    
                    // Put player inside vehicle
                    playerPed.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                    
                    GTA.UI.Notification.Show($"Spawned {modelName}!");
                }
            }
            model.MarkAsNoLongerNeeded();
        }
        else
        {
            GTA.UI.Notification.Show($"Model {modelName} is invalid.");
        }
    }
}
