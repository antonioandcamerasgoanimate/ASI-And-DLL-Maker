#include "script.h"
#include <windows.h>

void ScriptMain()
{
    while (true)
    {
        // Check if F9 key is pressed
        if (GetAsyncKeyState(VK_F9) & 0x8000)
        {
            // F9 pressed: Player invincibility toggle
            Player player = PLAYER::PLAYER_ID();
            BOOL isInvincible = PLAYER::IS_PLAYER_INVINCIBLE(player);
            PLAYER::SET_PLAYER_INVINCIBLE(player, !isInvincible);
            
            // Wait to prevent rapid toggling
            scriptWait(500);
        }

        // Must call scriptWait to let the game engine run
        scriptWait(0);
    }
}
