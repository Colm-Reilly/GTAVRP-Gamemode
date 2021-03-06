﻿using System.Timers;
using GrandTheftMultiplayer.Server.API;
using GrandTheftMultiplayer.Server.Elements;
using GrandTheftMultiplayer.Server.Managers;
using GrandTheftMultiplayer.Shared;
using mtgvrp.core;
using mtgvrp.inventory;
using mtgvrp.player_manager;
using mtgvrp.property_system;
using mtgvrp.vehicle_manager;
using Vehicle = mtgvrp.vehicle_manager.Vehicle;
using mtgvrp.core.Help;

namespace mtgvrp.speed_fuel_system
{
    class SpeedoFuelManager : Script
    {
        public Timer FuelTimer;

        public SpeedoFuelManager()
        {
            FuelTimer = new Timer(53000);
            FuelTimer.Elapsed += FuelTimer_Elapsed;
            FuelTimer.Start();

            API.onClientEventTrigger += API_onClientEventTrigger;
            API.onPlayerExitVehicle += API_onPlayerExitVehicle;
        }

        private void API_onClientEventTrigger(Client sender, string eventName, params object[] arguments)
        {
            if (eventName == "fuel_getvehiclefuel" && API.isPlayerInAnyVehicle(sender) &&
                API.getPlayerVehicleSeat(sender) == -1)
            {
                Vehicle veh = API.getEntityData(API.getPlayerVehicle(sender), "Vehicle");
                API.triggerClientEvent(sender, "fuel_updatevalue", veh.Fuel);
            }
        }

        private void FuelTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var veh in VehicleManager.Vehicles)
            {
                if (!veh.IsSpawned) continue;
                if (API.getVehicleEngineStatus(veh.NetHandle) != true || veh.Fuel <= 0) continue;
                if (API.shared.getVehicleClass(veh.VehModel) == 13) continue; //Skip cycles

                var ocups = API.getVehicleOccupants(veh.NetHandle);

                //Reduce fuel by one.
                veh.Fuel -= 1;
                if (veh.Fuel <= 0)
                {
                    API.setVehicleEngineStatus(veh.NetHandle, false);
                    if (ocups.Length > 0)
                        API.sendChatMessageToPlayer(ocups[0], "~y~The vehicle fuel has finished.");
                }

                //Notify driver with loss of fuel.
                if (ocups.Length > 0)
                {
                    API.triggerClientEvent(ocups[0], "fuel_updatevalue", veh.Fuel);
                }
            }
        }

        [Command("togspeedo"), Help(HelpManager.CommandGroups.Vehicles, "Used to find your character statistics", null)]
        public void TogSpeedo(Client player)
        {
            Account a = player.GetAccount();
            a.IsSpeedoOn = !a.IsSpeedoOn;
            API.sendChatMessageToPlayer(player, a.IsSpeedoOn ? "You've sucessfully turned on the speedometer." : "You've sucessfully turned off the speedometer.");
            a.Save();

            if (player.isInVehicle)
            {
                API.triggerClientEvent(player, "TOGGLE_SPEEDO");
            }
        }

        [Command("refuel"), Help(HelpManager.CommandGroups.Vehicles, "Command to refuel your vehicle from a gas station.", new[] { "Fuel amount wanted (out of 100)" })]
        public void Refuel(Client player, int fuel = 0)
        {
            var prop = PropertyManager.IsAtPropertyInteraction(player);
            if (prop?.Type == PropertyManager.PropertyTypes.GasStation)
            {
                if (API.isPlayerInAnyVehicle(player) && API.getPlayerVehicleSeat(player) == -1)
                {
                    var vehEntity = API.getPlayerVehicle(player);
                    Vehicle veh = API.getEntityData(vehEntity, "Vehicle");

                    if (API.getVehicleEngineStatus(vehEntity))
                    {
                        API.sendChatMessageToPlayer(player, "Vehicle engine must be off.");
                        return;
                    }

                    if (player.hasData("FUELING_VEHICLE"))
                    {
                        API.sendChatMessageToPlayer(player, "You're already refueling a vehicle.");
                        return;
                    }

                    if (fuel == 0)
                        fuel = 100 - veh.Fuel;

                    var pendingFuel = fuel;

                    if (pendingFuel > 100 || pendingFuel + veh.Fuel > 100 || pendingFuel < 0)
                    {
                        API.sendChatMessageToPlayer(player, "Vehicle fuel can't be above 100 or negative.");
                        return;
                    }

                    if (Money.GetCharacterMoney(player.GetCharacter()) < pendingFuel * prop.ItemPrices["gas"] && player.GetCharacter().Group.CommandType != group_manager.Group.CommandTypeLspd)
                    {
                        API.sendChatMessageToPlayer(player,
                            $"You don't have enough money to get ~r~{pendingFuel}~w~ units of fuel.~n~It's worth ~g~${pendingFuel * prop.ItemPrices["gas"]}~w~.");
                        return;
                    }

                    API.sendChatMessageToPlayer(player,
                        $"You will be charged ~g~${pendingFuel * prop.ItemPrices["gas"]}~w~ for ~r~{pendingFuel}~w~ units of fuel.");
                    API.freezePlayer(player, true);
                    API.setEntityData(vehEntity, "PENDING_FUEL", pendingFuel);
                    veh.RefuelProp = prop;
                    FuelVeh(new[] { player, vehEntity });
                    if (API.hasEntityData(vehEntity, "PENDING_FUEL"))
                    {
                        API.setEntityData(player, "FUELING_VEHICLE", vehEntity);
                        veh.FuelingTimer = new System.Threading.Timer(FuelVeh, new[] { player, vehEntity }, 3000, 3000);
                        return;
                    }
                }
                else
                {
                    API.sendChatMessageToPlayer(player, "You must be driving a vehicle.");
                }
            }
            else
            {
                API.sendChatMessageToPlayer(player, "You must be at a gas station.");
            }
        }

        private void API_onPlayerExitVehicle(Client player, NetHandle vehicle, int seat)
        {
            if (API.hasEntityData(player, "FUELING_VEHICLE"))
            {
                var vehEntity = API.getEntityData(player, "FUELING_VEHICLE");
                API.sendChatMessageToPlayer(player, "Refuel ended.");
                Vehicle veh = API.getEntityData(vehEntity, "Vehicle");
                veh.FuelingTimer?.Dispose();
                API.freezePlayer(player, false);
                veh.Save();
            }
        }

        private void FuelVeh(System.Object vars)
        {
            var handles = (NetHandle[])vars;
            Client playerEntity = API.getPlayerFromHandle(handles[0]);
            NetHandle vehEntity = handles[1];

            if (vehEntity.IsNull)
            {
                return;
            }

            Vehicle veh = API.getEntityData(vehEntity, "Vehicle");

            if (veh == null)
            {
                return;
            }

            if (playerEntity == null)
            {
                veh.FuelingTimer?.Dispose();
                API.resetEntityData(vehEntity, "PENDING_FUEL");
                return;
            }

            Character c = playerEntity.GetCharacter();

            if (c == null)
            {
                veh.FuelingTimer?.Dispose();
                API.resetEntityData(vehEntity, "PENDING_FUEL");
                return;
            }

            if (API.getVehicleEngineStatus(vehEntity))
            {
                veh.FuelingTimer?.Dispose();
                API.resetEntityData(vehEntity, "PENDING_FUEL");
                API.resetEntityData(playerEntity, "FUELING_VEHICLE");
                API.freezePlayer(playerEntity, false);
                API.sendChatMessageToPlayer(playerEntity, "Refuel has been cancelled cause the engine has turned on.");
                veh.Save();
                return;
            }

            int pendingFuel = API.getEntityData(vehEntity, "PENDING_FUEL") ?? 0;

            if (pendingFuel <= 0 || veh.RefuelProp.Supplies <= 0)
            {
                API.triggerClientEvent(playerEntity, "fuel_updatevalue", veh.Fuel);
                veh.FuelingTimer?.Dispose();
                API.resetEntityData(vehEntity, "PENDING_FUEL");
                API.resetEntityData(playerEntity, "FUELING_VEHICLE");
                API.freezePlayer(playerEntity, false);

                if(veh.RefuelProp.Supplies <= 0)
                    API.sendChatMessageToPlayer(playerEntity, "The gas station ran out of gas.");
                else if (pendingFuel <= 0)
                    API.sendChatMessageToPlayer(playerEntity, "Refueling finished.");

                veh.Save();
                return;
            }

            if (pendingFuel < 10)
            {
                veh.Fuel += pendingFuel;
                pendingFuel -= pendingFuel;
                if (c.Group.CommandType != group_manager.Group.CommandTypeLspd)
                {
                    InventoryManager.DeleteInventoryItem<Money>(c, pendingFuel * veh.RefuelProp.ItemPrices["gas"]);
                }
                veh.RefuelProp.Supplies--;
            }
            else
            {
                veh.Fuel += 10;
                pendingFuel -= 10;
                if (c.Group.CommandType != group_manager.Group.CommandTypeLspd)
                {
                    InventoryManager.DeleteInventoryItem<Money>(c, 10 * veh.RefuelProp.ItemPrices["gas"]);
                }
                veh.RefuelProp.Supplies--;
            }

            API.triggerClientEvent(playerEntity, "fuel_updatevalue", veh.Fuel);
            API.setEntityData(vehEntity, "PENDING_FUEL", pendingFuel);
        }
    }
}