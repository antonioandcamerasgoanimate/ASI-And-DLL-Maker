using System;
using System.Windows.Forms;
using GTA;

public class BaseScript : Script
{
    public BaseScript()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown;
    }

    private void OnTick(object sender, EventArgs e)
    {
        // Add tick-based loop logic here (runs every frame)
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F9)
        {
            // Toggle player invincibility
            var player = Game.Player;
            player.IsInvincible = !player.IsInvincible;

            GTA.UI.Notification.Show($"Invincibility: {(player.IsInvincible ? "ON" : "OFF")}");
        }
    }
}
