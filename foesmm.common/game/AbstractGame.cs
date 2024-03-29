﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace foesmm.common.game
{
    public abstract class AbstractGame : IGame
    {
        public Guid Guid => new Guid(
            ((GuidAttribute)GetType().Assembly.GetCustomAttributes(typeof(GuidAttribute), true)[0]).Value);

        public abstract World World { get; }

        public abstract ReleaseState ReleaseState { get; }
        public abstract string Title { get; }
        public abstract string ShortTitle { get; }
        public abstract int ReleaseYear { get; }
        public BitmapImage Cover => new BitmapImage(new Uri($"pack://application:,,,/{GetType().Assembly.GetName().Name};component/Resources/cover.jpg"));

        public abstract string Executable { get; }
        public string InstallPath { get; protected set; }
        public string Version => InstallPath != null ? FileVersionInfo.GetVersionInfo(Path.Combine(InstallPath, Executable)).FileVersion : null;
        public string DataPath => InstallPath != null ? Path.Combine(InstallPath, "Data") : null;

        public abstract IToolKit ToolKit { get; }
        public abstract IScriptExtender ScriptExtender { get; }
        public abstract ISaveManager SaveManager { get; }

        public string CrashTrace => $"Game: {Title} ({ReleaseState})\n";

        protected abstract string[] GOGKeys { get; }
        protected abstract string[] SteamKeys { get; }
        protected abstract Dictionary<string, string> RetailKeys { get; }

        public override string ToString()
        {
            return Title;
        }

        public bool IsLargeAddressAware()
        {
            if (InstallPath == null) return false;
            using (var stream = File.OpenRead(Path.Combine(InstallPath, Executable)))
            {
                const int imageFileLargeAddressAware = 0x20;
                const int imageFileIs64Bit = 0x8664;

                var br = new BinaryReader(stream);

                if (br.ReadInt16() != 0x5A4D)       //No MZ Header
                    return false;

                br.BaseStream.Position = 0x3C;
                var peloc = br.ReadInt32();         //Get the PE header location.

                br.BaseStream.Position = peloc;
                if (br.ReadInt32() != 0x4550)       //No PE header
                    return false;

                br.BaseStream.Position += 0x12;
                var result = br.ReadInt16();
                return (result & imageFileLargeAddressAware) == imageFileLargeAddressAware ||
                    (result & imageFileIs64Bit) == imageFileIs64Bit;
            }
        }

        public static IGame LoadGame(Type type, string path)
        {
            var game = (AbstractGame)Activator.CreateInstance(type);
            return game.Instantinate(path) ? game : null;
        }

        protected bool Instantinate(string path)
        {
            if (!IsValidGameFolder(path))
                return false;

            InstallPath = path;

            RefreshContent();

            return true;
        }

        protected bool IsValidGameFolder(string path)
        {
            if (path == null) return false;
            return File.Exists(Path.Combine(path, Executable));
        }

        public void RefreshContent()
        {

        }

        protected AbstractGame()
        {
            Plugins = new List<IPlugin>();
            Archives = new List<IArchive>();
        }

        protected void DetectInstallationGOG()
        {
            InstallPath = RegistryHelper.CheckInstallInfo(@"Software\GOG.com\Games", GOGKeys, "path");
        }

        protected void DetectInstallationSteam()
        {
            //
            InstallPath = RegistryHelper.CheckInstallInfo(@"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                SteamKeys, "InstallLocation");
        }

        protected void DetectInstallationRetail()
        {
//                RegistryHelper.CheckInstallInfo(@"\", RetailKeys, "");
        }

        public bool DetectInstallation(string installPath = null)
        {
            if (installPath != null && IsValidGameFolder(installPath))
            {
                InstallPath = installPath;
                return true;
            }

            if (IsValidGameFolder(InstallPath)) return true;
            DetectInstallationGOG();
            DetectInstallationSteam();
            DetectInstallationRetail();
            return IsValidGameFolder(InstallPath);
        }

        public IList<IPlugin> Plugins { get; protected set; }
        public IList<IArchive> Archives { get; protected set; }
        public IDictionary<IPlugin, IArchive> Links { get; protected set; }
    }
}
