/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

// Altered source: embedded library mode and offline UI demonstration.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using SAM.UI;

namespace SAM.Game
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SAM.Picker.exe"));
                return;
            }
            if (!long.TryParse(args[0], out var appId) || appId <= 0 || appId > uint.MaxValue)
            { MessageBox.Show("App ID Steam invalide."); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool demo = Array.IndexOf(args, "--demo") >= 0;
            using var client = demo ? null : new API.Client();
            if (!demo)
            {
                if (API.Steam.GetInstallPath() == Application.StartupPath)
                { MessageBox.Show("Placez cette application hors du dossier Steam."); return; }
                try { client.Initialize(appId); }
                catch (Exception e) when (e is API.ClientInitializeException || e is DllNotFoundException || e is BadImageFormatException)
                { MessageBox.Show("Impossible de se connecter à Steam. Vérifiez que Steam est ouvert et votre compte connecté.\n" + e.Message, "Connexion Steam"); return; }
            }
            using var manager = new Manager(appId, client, demo);
            int hostIndex = Array.IndexOf(args, "--host");
            int ownerIndex = Array.IndexOf(args, "--owner");
            if (hostIndex >= 0 && ownerIndex >= 0 && hostIndex + 1 < args.Length && ownerIndex + 1 < args.Length &&
                long.TryParse(args[hostIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long host) &&
                int.TryParse(args[ownerIndex + 1], out int owner) && host != 0 && owner > 0)
                WindowHost.Attach(manager, new IntPtr(host), owner);
            Application.Run(manager);
        }
    }
}
