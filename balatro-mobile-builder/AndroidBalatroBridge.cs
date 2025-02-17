﻿using System.Globalization;

namespace BalatroMobileBuilder
{
    public class AndroidBalatroBridge
    {
        public static readonly string packageName = "com.unofficial.balatro";
        public ExternalTool.ADB adb;

        public AndroidBalatroBridge() {
            this.adb = new ExternalTool.ADB();
        }

        public async Task downloadMissing() {
            if (adb.path != null) return;
            await adb.downloadTool();
        }

        public void askToDeleteTools(bool silentMode) {
            if (adb.wasDownloaded && ConInterface.ask("Delete ADB", silentMode, true)) {
                adb.deleteTool();
            }
        }

        public void installApk(string signedApk) {
            int result = adb.install(signedApk);
            if (result != 0) {
                ConInterface.printError($"{adb.name} returned {result}");
                Environment.Exit(result);
            }
            adb.killServer();
        }

        public bool copySaveToDevice(int saveNum, bool ignoreNonExistent = true, string? savePath = null) {
            if (savePath == null)
                savePath = BalatroSaveReader.getLocalSavePath();
            if (!Directory.Exists($"{savePath}/{saveNum}")) {
                if (ignoreNonExistent) return true;
                ConInterface.printError("Couldn't find save directory.");
                return false;
            }

            // Clean and prepare folders
            adb.runShell("rm -r /data/local/tmp/balatro/");
            adb.runShell($"mkdir -p /data/local/tmp/balatro/{saveNum}/");
            adb.runShell($"mkdir -p ./files/save/game/{saveNum}/", packageName);
            // Copy
            int errCheck = adb.push($"{savePath}/{saveNum}/.", $"/data/local/tmp/balatro/{saveNum}/");
            adb.runShell($"am force-stop {packageName}"); // Stop Balatro process
            adb.runShell($"rm ./files/save/game/{saveNum}/save.jkr", packageName); // Remove save.jkr as it may not be present (and not overwritten)
            errCheck |= adb.runShell($"cp -r /data/local/tmp/balatro/{saveNum} ./files/save/game", packageName)?.ExitCode ?? 1;

            return errCheck == 0;
        }

        public bool copySaveFromDevice(int saveNum, bool ignoreNonExistent = true, string? savePath = null) {
            if (savePath == null)
                savePath = BalatroSaveReader.getLocalSavePath();
            if (!Directory.Exists(savePath)) {
                ConInterface.printError("Couldn't find save directory.");
                return false;
            }

            // Clean and prepare folders
            Directory.CreateDirectory($"{savePath}/{saveNum}");
            adb.runShell("rm -r /data/local/tmp/balatro/");
            adb.runShell($"mkdir -p /data/local/tmp/balatro/{saveNum}/");
            adb.runShell($"mkdir -p ./files/save/game/{saveNum}/", packageName);
            // Copy
            int errCheck = adb.runShell($"cat ./files/save/game/{saveNum}/profile.jkr > /data/local/tmp/balatro/{saveNum}/profile.jkr", packageName)?.ExitCode ?? 1;
            errCheck |= adb.runShell($"cat ./files/save/game/{saveNum}/meta.jkr > /data/local/tmp/balatro/{saveNum}/meta.jkr", packageName)?.ExitCode ?? 1;
            if (ignoreNonExistent && errCheck != 0)
                return true;
            adb.runShell($"cat ./files/save/game/{saveNum}/save.jkr > /data/local/tmp/balatro/{saveNum}/save.jkr", packageName);
            File.Delete($"{savePath}/{saveNum}/save.jkr"); // Remove save.jkr as it may not be present (and not overwritten)
            errCheck |= adb.pull($"/data/local/tmp/balatro/{saveNum}/.", $"{savePath}/{saveNum}/");

            return errCheck == 0;
        }

        public BalatroSaveReader? readSaveFile(int saveNum, string type) {
            // Read file as hex string using xxd and then convert to binary.
            // By doing it this way, we avoid pulling through ADB.
            string? hex = null;
            adb.runShell($"xxd -c 0 -p files/save/game/{saveNum}/{type}.jkr", out hex, packageName);
            if (!string.IsNullOrEmpty(hex)) {
                hex = hex.Trim();
                byte[] fileContent = Enumerable.Range(0, hex.Length / 2)
                    .Select(x => Byte.Parse(hex.Substring(2 * x, 2), NumberStyles.HexNumber))
                    .ToArray();
                using (MemoryStream fileStream = new MemoryStream(fileContent)) {
                    return new BalatroSaveReader(fileStream);
                }
            }
            return null;
        }
    }
}
