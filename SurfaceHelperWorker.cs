using Observatory.Framework;
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

        private class BodyInfo {
            public double gravity;
            public double temp;
            public BodyInfo(double grav, double temp) {
                this.gravity = grav;
                this.temp = temp;
            }
        }


        private IObservatoryCore Core;
        private bool OdysseyLoaded = false;

        private NotificationArgs currentNotificationArgs = null;
        private (double lat, double lon) shipLocation = INVALID_LOCATION;
        private (double lat, double lon) cockpitLocation = INVALID_LOCATION;
        private double currentBodyRadius = 0;
        private bool tracking = false;
        private int shipDistanceRangeNow = 0; // 0 - near, 1,2 - outside ranges.
        private int statsSinceOnLand = 0;

        private string currentSystemName = "";
        private ulong currentSystemId = 0;
        private string currentBodyName = "";
        private int currentBodyId = -1;
        private Dictionary<int, BodyInfo> bodies = new Dictionary<int, BodyInfo>();


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
                    Logger.AppendLog($"LoadGame: StartLanded {loadGame.StartLanded}", settings.LogFile);
                    OdysseyLoaded = loadGame.Odyssey;
                    //onLoadGame(loadGame);
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
                case ApproachBody approachBody:
                    onApproachBody(approachBody);
                    break;
                case SupercruiseExit supercruiseExit:
                    onSupercruiseExit(supercruiseExit);
                    break;
                case Scan scan:
                    onScan(scan);
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

        private void onScan(Scan scan) {
            //Logger.AppendLog($"Scan: Gravity {scan.SurfaceGravity}, Temp {scan.SurfaceTemperature}, Landable {scan.Landable}", settings.LogFile);
            //Logger.AppendLog($"Scan: StarSys [{scan.StarSystem}], SysAddr {scan.SystemAddress}, BodyID {scan.BodyID}", settings.LogFile);
            checkNewSystem(scan.SystemAddress, scan.StarSystem);
            Logger.AppendLog($"Scan: body ID={scan.BodyID}, grav {scan.SurfaceGravity}, temp {scan.SurfaceTemperature}", settings.LogFile);
            bodies[scan.BodyID] = new BodyInfo(scan.SurfaceGravity, scan.SurfaceTemperature);
        }

        private void onSupercruiseExit(SupercruiseExit supercruiseExit) {
            //Logger.AppendLog($"SupercruiseExit: Body {supercruiseExit.Body}, BodyID {supercruiseExit.BodyID}, BodyType {supercruiseExit.BodyType}", settings.LogFile);
            // There's no system info, obly body info.
            if (settings.SCExitWelcome) {
                planetWelcome(supercruiseExit.BodyID, supercruiseExit.Body);
            }
        }

        private void onApproachBody(ApproachBody approachBody) {
            //Logger.AppendLog($"ApproachBody: StarSys {approachBody.StarSystem}, SysAddr {approachBody.SystemAddress}, Body {approachBody.Body}, BodyID {approachBody.BodyID}", settings.LogFile);
            checkNewSystem(approachBody.SystemAddress, approachBody.StarSystem);
            if (settings.ApproachWelcome) {
                planetWelcome(approachBody.BodyID, approachBody.Body);
            }
        }

        private void checkNewSystem(ulong systemId, string systemName) {
            if (currentSystemId != systemId) {
                Logger.AppendLog($"New system: [{systemName}] ID={systemId}", settings.LogFile);
                currentSystemName = systemName;
                currentSystemId = systemId;
                bodies.Clear();
            }
        }

        private void planetWelcome(int bodyId, string fullBodyName) {
            Logger.AppendLog($"Welcome: body #{currentBodyId} ({currentBodyName})", settings.LogFile);
            currentBodyId = bodyId;
            currentBodyName = extractBodyName(currentSystemName, fullBodyName);
            BodyInfo info;
            if (bodies.TryGetValue(bodyId, out info)) {
                Logger.AppendLog($"Welcome: body {currentBodyId} ({currentBodyName}), {info.temp}°K / {info.gravity}G", settings.LogFile);
                var gravityStr = (info.gravity >= settings.HighGravity) ? "High gravity! " : "Gravity: ";
                var tempStr = (info.temp >= settings.HighTemperature) ? "Hight temperature! " : "Temperature: ";
                double temp;
                switch (settings.TemperatureScale) {
                    default: temp = info.temp; break;
                    case 1: temp = MathHelper.kelvinToCelsius(info.temp); break;
                    case 2: temp = MathHelper.kelvinToFarenheit(info.temp); break;
                };
                string degStr = "degrees";
                if (settings.TemperatureScaleName) switch (settings.TemperatureScale) {
                    case 0: degStr = $"degrees Kelvin"; break;
                    case 1: degStr = $"degrees Celsius"; break;
                    case 2: degStr = $"degrees Fahrenheit"; break;
                }
                int roundedTemp = (int)(Math.Round(temp / 25)) * 25;
                double gravity = Math.Round(info.gravity / 9.81f, 1);
                Logger.AppendLog($"Welcome: #{currentBodyId} ({currentBodyName}), {roundedTemp} {degStr} / {gravity} G", settings.LogFile);
                showPlanetWelcomeNotification($"{gravityStr}{gravity} G.\n{tempStr}{roundedTemp} {degStr}.");
            } else {
                Logger.AppendLog($"Welcome: unscanned body #{3} ({currentBodyName})", settings.LogFile);
            }
        }

        private string extractBodyName(string starSystem, string fullBodyName)
            => fullBodyName.Replace(starSystem, "").Trim();

        private void showPlanetWelcomeNotification(string text) {
            NotificationArgs args = new() {
                Title = $"Welcome to body {currentBodyName}!",
                TitleSsml = $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name=\"\"></voice></speak>",
                Detail = text,
                DetailSsml = $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name=\"\">Welcome to body {currentBodyName}!\n{text}</voice></speak>",
                Rendering = NotificationRendering.All,
                Timeout = -1, //settings.OverlayIsSticky ? 0 : -1,
                Sender = AboutInfo.ShortName,
                Guid = Guid.NewGuid(),
            };
            Core.SendNotification(args);
        }

        /**
          EVENT:       Ship landed.
          ASSUMPTIONS: Player could be inside or outside the ship!
          ACTION:      Save it's coordinates. But it is not exact unfortunately :(
        **/
        private void onTouchdown(Touchdown touchdown)
        {
            //Logger.AppendLog($"Touchdown: NearDest {touchdown.NearestDestination}, StarSys {touchdown.StarSystem}, SysAddr {touchdown.SystemAddress}, Body {touchdown.Body}, BodyID {touchdown.BodyID}", settings.LogFile);

            if (touchdown.OnStation || !touchdown.OnPlanet || touchdown.Taxi)
            {
                MaybeCloseNotification();
                return;
            }
            Logger.AppendLog($"Touchdown: LAT {touchdown.Latitude}, LON {touchdown.Longitude}", settings.LogFile);
            if (!touchdown.PlayerControlled)
            {
                // Ship was recalled and landed automatically. Player is OUTSIDE ship now.
                // TODO: Check if this is ship center location or cockpit again.
                shipLocation = (touchdown.Latitude, touchdown.Longitude);
                cockpitLocation = INVALID_LOCATION; // Now assume it is real ship location.
                startTracking();
            }
            else
            {
                // Ship landed with player INSIDE.
                // Unfortuinately, this is player inside cockpit location, not ship center :(
                cockpitLocation = (touchdown.Latitude, touchdown.Longitude);
            }

            checkNewSystem(touchdown.SystemAddress, touchdown.StarSystem);
            if (settings.TouchdownWelcome) {
                planetWelcome(touchdown.BodyID, touchdown.Body);
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
            cockpitLocation = INVALID_LOCATION;
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

            checkNewSystem(disembark.SystemAddress, disembark.StarSystem);
            if (settings.TouchdownWelcome) {
                planetWelcome(disembark.BodyID, disembark.Body);
            }
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
            if (!tracking) return;
            if (shipLocation == INVALID_LOCATION) {
                statsSinceOnLand++;
                // Skip first 3 locations because it's wrong.
                if (statsSinceOnLand > 3) {
                    guessShipLocationAtDisembark((status.Latitude, status.Longitude));
                }
            }
            processNewLocation(status);
        }
        
        
        private void guessShipLocationAtDisembark((double lat, double lon) player) {
            Logger.AppendLog($"Guessing ship location: Disembark at (LAT: {player.lat}, LON: {player.lon})", settings.LogFile);
            if (shipLocation != INVALID_LOCATION) {
                // We already know ship lication, for example from Touchdown without pilot.
                Logger.AppendLog($"KEEP SHIP LOCATION: LAT: {shipLocation.lat}, LON:{shipLocation.lon}", settings.LogFile);
                return;
            }
            if (cockpitLocation != INVALID_LOCATION) {
                shipLocation = MathHelper.middlePoint(cockpitLocation, player, settings.ShipCenterOffset);
                Logger.AppendLog($"SAVING SHIP WITH CORRECTION: LAT: {shipLocation.lat}, LON:{shipLocation.lon}", settings.LogFile);
            } else {
                Logger.AppendLog($"SAVING SHIP DIRECTLY", settings.LogFile);
                shipLocation = player;
            }
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
            return MathHelper.calculateGreatCircleDistance(shipLocation, (status.Latitude, status.Longitude), status.PlanetRadius);
        }

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
            if (settings.LogFile == SurfaceHelperSettings.DEFAULT_LOG_NAME) {
                settings.LogFile = observatoryCore.PluginStorageFolder + SurfaceHelperSettings.DEFAULT_LOG_NAME;
            }

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
