﻿using Observatory.Framework;
using Observatory.Framework.Files;
using Observatory.Framework.Files.Journal;
using Observatory.Framework.Files.ParameterTypes;
using Observatory.Framework.Interfaces;
using Observatory.Framework.Sorters;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Observatory.SurfaceHelper {
    public class SurfaceHelperWorker : IObservatoryWorker {
        private static (double lat, double lon) INVALID_LOCATION = (200, 200);


        private IObservatoryCore Core;
        private bool OdysseyLoaded = false;

        private NotificationArgs currentNotificationArgs = null;
        private (double lat, double lon) shipLocation = INVALID_LOCATION;
        private double currentBodyRadius = 0;
        private bool tracking = false;
        private int shipDistanceRangeNow = 0; // 0 - near, 1,2 - outside ranges.
        private int statsSinceOnLand = 0;


        private SurfaceHelperSettings settings = SurfaceHelperSettings.DEFAULT;
        ObservableCollection<object> GridCollection;
        private PluginUI pluginUI;

        private AboutInfo _aboutInfo = new()
        {
            FullName = "Surface Helper",
            ShortName = "SurfaceHelper",
            Description = "SurfaceHelper helps to track 2km distance to ship.",
            AuthorName = "G-Shadow",
            Links = new()
            {
                new AboutLink("GitHub", "https://github.com/gshadows/SurfaceHelper"),
                //new AboutLink("Documentation", "https://github.com/gshadows/SurfaceHelper"),
            }
        };

        public AboutInfo AboutInfo => _aboutInfo;

        public string Version => typeof(SurfaceHelperWorker).Assembly.GetName().Version.ToString();

        public PluginUI PluginUI => pluginUI;

        public object Settings { get => settings; set { settings = (SurfaceHelperSettings)value;  } }


        public void JournalEvent<TJournal>(TJournal journal) where TJournal : JournalBase
        {
            switch (journal) {
                case LoadGame loadGame:
                    OdysseyLoaded = loadGame.Odyssey;
                    break;
                case Liftoff liftoff:
                    onLiftoff(liftoff);
                    break;
                case Touchdown touchdown:
                    onTouchdown(touchdown);
                    break;
                case Location location:
                    onLocation(location);
                    break;
                case Embark embark:
                    onEmbark(embark);
                    break;
                case Disembark disembark:
                    onDisembark(disembark);
                    break;
                case LaunchSRV launchSrv:
                    onLaunchSRV(launchSrv);
                    break;
                case DockSRV dockSrv:
                    onDockSRV(dockSrv);
                    break;
                case LeaveBody:
                case FSDJump:
                case Shutdown:
                case SupercruiseEntry:
                    MaybeCloseNotification();
                    tracking = false;
                    break;
            }
        }

        /**
          EVENT:       Ship landed.
          ASSUMPTIONS: Player could be inside or outside the ship!
          ACTION:      Save it's coordinates.
        **/
        private void onTouchdown(Touchdown touchdown) {
            if (touchdown.OnStation || !touchdown.OnPlanet || touchdown.Taxi) {
                MaybeCloseNotification();
                return;
            }
            //shipLocation = (touchdown.Latitude, touchdown.Longitude); -- wrong, this is Pilot's coords sitting in chair.
            Logger.AppendLog($"Touchdown: LAT {touchdown.Latitude}, LON {touchdown.Longitude}", settings.LogFile);
            if (!touchdown.PlayerControlled) {
                shipLocation = (touchdown.Latitude, touchdown.Longitude);
                startTracking();
            }
        }

        /**
          EVENT:       Ship took off.
          ASSUMPTIONS: Player can be insed or outside.
          ACTION:      Forget it's coordinates & stop tracking.
        **/
        private void onLiftoff(Liftoff liftoff) {
            Logger.AppendLog("Liftoff", settings.LogFile);
            stopTracking();
            shipLocation = INVALID_LOCATION;
            statsSinceOnLand = 0;
            if (!liftoff.PlayerControlled) {
                // Ship flew away without player T_T
                showShipLeftNotification();
            }
        }

        /**
          EVENT:       SRV has been launched from the ship.
          ASSUMPTIONS: Player was inside -> player left the ship.
          ACTION:      Start tracking.
        **/
        private void onLaunchSRV(LaunchSRV launchSRV) {
            Logger.AppendLog("LaunchSRV", settings.LogFile);
            startTracking();
        }

        /**
          EVENT:       Player disembarked from the ship or SRV.
          ASSUMPTIONS: if not SRV, then player just left the ship.
          ACTION:      Start tracking if applicable, except SRV disembark.
        **/
        private void onDisembark(Disembark disembark) {
            if (disembark.OnStation || !disembark.OnPlanet || disembark.Taxi || disembark.SRV) {
                return;
            }
            Logger.AppendLog("Disembark", settings.LogFile);
            startTracking();
        }

        /**
          EVENT:       SRV has been docked to the ship.
          ASSUMPTIONS: Player was inside -> player now inside the ship. Ship still landed.
          ACTION:      Stop tracking.
        **/
        private void onDockSRV(DockSRV dockSRV) {
            Logger.AppendLog("DockSRV", settings.LogFile);
            stopTracking();
        }
        
        /**
          EVENT:       Player returned back to the ship.
          ASSUMPTIONS: Player now inside the ship. Ship still landed.
          ACTION:      Stop tracking.
        **/
        private void onEmbark(Embark embark) {
            if (embark.SRV) {
                return;
            }
            Logger.AppendLog("Embark", settings.LogFile);
            stopTracking();
        }

        /**
          EVENT:       Planet surface location updated. (no idea when it called)
          ASSUMPTIONS: -
          ACTION:      Update location and do all required checks if tracking active and on surface.
        **/
        private void onLocation(Location location) {
            Logger.AppendLog("Location", settings.LogFile);
            if ((!location.OnFoot && !location.InSRV) || location.Taxi || location.Docked) return;
            if (!tracking) return;

            var status = Core.GetStatus();
            if (status == null) {
                status = new() {
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    PlanetRadius = currentBodyRadius,
                };
            } else {
                currentBodyRadius = status.PlanetRadius;
            }
            processNewLocation(status);
        }


        public void StatusChange(Status status) {
            Logger.AppendLog("StatusChange", settings.LogFile);
            if (!tracking) return;
            if (shipLocation == INVALID_LOCATION) {
                statsSinceOnLand++;
                if (statsSinceOnLand > 3) {
                    Logger.AppendLog($"SAVING SHIP COORD: LAT: {status.Latitude}, LON:{status.Longitude}, PR {status.PlanetRadius}", settings.LogFile);
                    shipLocation = (status.Latitude, status.Longitude);
                }
            }
            processNewLocation(status);
        }


        private void processNewLocation(Status status) {
            Logger.AppendLog($"LAT: {status.Latitude}, LON:{status.Longitude}, PR {status.PlanetRadius}", settings.LogFile);

            var distance = GetDistance(status);
            Logger.AppendLog($"  distance = {distance}", settings.LogFile);
            if (double.IsNaN(distance)) {
                MaybeCloseNotification();
                return;
            }
            NotifyDistanceLimits(distance);

            // Show or update notification.
            bool isFirstTime = currentNotificationArgs == null;
            var notificationGuid = isFirstTime ? Guid.NewGuid() : currentNotificationArgs.Guid;

            currentNotificationArgs = new() {
                Title = "Ship Distance",
                Detail = GetDistanceText(distance),
                Rendering = NotificationRendering.NativeVisual,
                Timeout = 0, //settings.OverlayIsSticky ? 0 : -1,
                Sender = AboutInfo.ShortName,
                Guid = notificationGuid,
                XPos = 0f,
                YPos = 50f,
            };
            if (isFirstTime) {
                Core.SendNotification(currentNotificationArgs);
            } else {
                Core.UpdateNotification(currentNotificationArgs);
            }
        }


        private void startTracking() {
            Logger.AppendLog("--- start tracking ---", settings.LogFile);
            tracking = true;
        }

        private void stopTracking() {
            Logger.AppendLog("--- stop tracking ---", settings.LogFile);
            MaybeCloseNotification();
            tracking = false;
        }


        private void showShipLeftNotification() {
            if (isSkipNotifications()) return;
            NotificationArgs args = new() {
                Title = "Ship lost!",
                TitleSsml = $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name=\"\"></voice></speak>",
                Detail = "Your ship just took off without you!",
                Rendering = NotificationRendering.All,
                Timeout = -1, //settings.OverlayIsSticky ? 0 : -1,
                Sender = AboutInfo.ShortName,
                Guid = Guid.NewGuid(),
            };
            Core.SendNotification(args);
        }

        private void showShipTooFarNotification(bool is2ndRange) {
            Logger.AppendLog("!!! Ship distance exceeded!!!", settings.LogFile);
            if (isSkipNotifications()) return;
            Logger.AppendLog($"!!! Ship distance exceeded: is2ndRange = {is2ndRange}", settings.LogFile);

            var range = is2ndRange ? settings.ShipDistance2 : settings.ShipDistance1;
            var rangeText = $"{range:N0} meters";

            NotificationArgs args = new() {
                Title = "Ship Distance!",
                TitleSsml = $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name=\"\"></voice></speak>",
                Detail = $"Ship distance is over {rangeText}, commander!",
                Rendering = NotificationRendering.All,
                Timeout = -1, //settings.OverlayIsSticky ? 0 : -1,
                Sender = AboutInfo.ShortName,
                Guid = Guid.NewGuid(),
            };
            Core.SendNotification(args);
        }


        private bool isSkipNotifications() => Core.IsLogMonitorBatchReading || !settings.OverlayEnabled;

        private void NotifyDistanceLimits(double distance) {
            if (distance >= settings.ShipDistance2) {
                if (shipDistanceRangeNow < 2) {
                    shipDistanceRangeNow = 2;
                    showShipTooFarNotification(true);
                }
                return;
            }
            if (distance >= settings.ShipDistance1) {
                if (shipDistanceRangeNow < 1) {
                    shipDistanceRangeNow = 1;
                    showShipTooFarNotification(false);
                }
                return;
            }
            shipDistanceRangeNow = 0;
        }

        private string GetDistanceText(double distance) => $"Ship distance: {distance:N0}m";

        private double GetDistance(Status status) {
            if (shipLocation == INVALID_LOCATION || status.PlanetRadius <= 0) {
                return double.NaN;
            }
            return CalculateGreatCircleDistance(shipLocation, (status.Latitude, status.Longitude), status.PlanetRadius);
        }

        private static double CalculateGreatCircleDistance((double lat, double lon) location1, (double lat, double lon) location2, double radius) {
            //Logger.AppendLog($"Calc dist: D.LAT {location2.lat - location1.lat}, D.LON {location2.lon - location1.lon}, RAD={radius}", settings.LogFile);

            var latDeltaSin = Math.Sin(ToRadians(location1.lat - location2.lat) / 2);
            var longDeltaSin = Math.Sin(ToRadians(location1.lon - location2.lon) / 2);
            
            var hSin = latDeltaSin * latDeltaSin + Math.Cos(ToRadians(location1.lat)) * Math.Cos(ToRadians(location1.lat)) * longDeltaSin * longDeltaSin;
            return Math.Abs(2 * radius * Math.Asin(Math.Sqrt(hSin)));
        }

        private static double ToRadians(double degrees) => degrees * 0.0174533;

        private void MaybeCloseNotification() {
            if (currentNotificationArgs != null) {
                Core.CancelNotification(currentNotificationArgs.Guid.Value);
                currentNotificationArgs = null;
            }
        }

        public void Load(IObservatoryCore observatoryCore) {
            GridCollection = new();
            SurfaceHelperGrid uiObject = new();
            GridCollection.Add(uiObject);
            pluginUI = new PluginUI(GridCollection);

            Core = observatoryCore;
        }

        public void LogMonitorStateChanged(LogMonitorStateChangedEventArgs args) {
        }
    }


    public class SurfaceHelperGrid {
        [ColumnSuggestedWidth(300)]
        public string Item { get; set; }

        [ColumnSuggestedWidth(300)]
        public string Distance { get; set; }

        [ColumnSuggestedWidth(300)]
        public string Direction { get; set; }
    }
}
