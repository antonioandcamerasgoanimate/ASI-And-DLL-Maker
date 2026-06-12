#include "script.h"
#include <windows.h>

void ScriptMain()
{
    while (true)
    {
        // Check if F10 key is pressed
        if (GetAsyncKeyState(VK_F10) & 0x8000)
        {
            Hash model = GAMEPLAY::GET_HASH_KEY("adder");

            if (STREAMING::IS_MODEL_IN_CDIMAGE(model) && STREAMING::IS_MODEL_A_VEHICLE(model))
            {
                // Request vehicle model
                STREAMING::REQUEST_MODEL(model);
                while (!STREAMING::HAS_MODEL_LOADED(model)) 
                {
                    scriptWait(0);
                }

                // Get player ped and coordinates
                Ped playerPed = PLAYER::PLAYER_PED_ID();
                Vector3 coords = ENTITY::GET_ENTITY_COORDS(playerPed, TRUE);

                // Create vehicle in front of player
                float heading = ENTITY::GET_ENTITY_HEADING(playerPed);
                float xOffset = -sin(heading * 3.14159f / 180.0f) * 5.0f;
                float yOffset = cos(heading * 3.14159f / 180.0f) * 5.0f;

                Vehicle veh = VEHICLE::CREATE_VEHICLE(
                    model, 
                    coords.x + xOffset, 
                    coords.y + yOffset, 
                    coords.z, 
                    heading, 
                    TRUE, 
                    FALSE
                );

                VEHICLE::SET_VEHICLE_ON_GROUND_PROPERLY(veh);
                
                // Release model
                STREAMING::SET_MODEL_AS_NO_LONGER_NEEDED(model);
            }

            // Prevent rapid spawning
            scriptWait(1000);
        }

        scriptWait(0);
    }
}
