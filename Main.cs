using Flow.Launcher.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.EdgeProfiles
{
    /// <summary>
    /// Represents an Edge profile.
    /// </summary>
    public class Profile
    {
        /// <summary>
        /// Gets or sets the name of the profile.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the directory of the profile.
        /// </summary>
        public string Directory { get; set; }

        /// <summary>
        /// Gets or sets the path to the profile icon.
        /// </summary>
        public string IcoPath { get; set; }
    }

    /// <summary>
    /// Main class for the Edge Profiles plugin.
    /// </summary>
    public class Main : IPlugin, IAsyncReloadable
    {
        private PluginInitContext _context;
        private List<Profile> _cachedProfiles = new List<Profile>();

        private static readonly string EdgeLocalState = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Edge", "User Data", "Local State"
        );

        private static readonly string[] EdgeExePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe")
        };

        private const string DefaultIconPath = "default_icon.png";

        /// <summary>
        /// Initializes the plugin with the given context.
        /// </summary>
        /// <param name="context">The plugin initialization context.</param>
        public void Init(PluginInitContext context)
        {
            _context = context;
            Task.Run(() => UpdateProfilesCache());
        }

        /// <summary>
        /// Queries the plugin with the given query.
        /// </summary>
        /// <param name="query">The query to process.</param>
        /// <returns>A list of results based on the query.</returns>
        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            foreach (var profile in _cachedProfiles)
            {
                if (!string.IsNullOrWhiteSpace(query.Search) && !profile.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
                    continue;

                var result = new Result
                {
                    Title = profile.Name,
                    SubTitle = $"Open Edge profile: {profile.Name}",
                    IcoPath = profile.IcoPath,
                    Action = _ =>
                    {
                        try
                        {
                            OpenOrSwitchProfile(profile.Name, profile.Directory);
                        }
                        catch (Exception ex)
                        {
                            _context.API.ShowMsg("Error", $"Failed to open or switch profile: {ex.Message}");
                        }
                        return true;
                    }
                };

                results.Add(result);
            }

            Task.Run(() => UpdateProfilesCache());

            return results;
        }

        /// <summary>
        /// Updates the profiles cache asynchronously.
        /// </summary>
        private async Task UpdateProfilesCache()
        {
            var profiles = await Task.Run(() => GetProfiles());
            _cachedProfiles = profiles;
        }

        /// <summary>
        /// Retrieves the list of Edge profiles.
        /// </summary>
        /// <returns>A list of profiles.</returns>
        private List<Profile> GetProfiles()
        {
            var profiles = new List<Profile>();

            if (!File.Exists(EdgeLocalState))
                return profiles;

            var json = File.ReadAllText(EdgeLocalState);
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.TryGetProperty("profile", out var profileElement)
                && profileElement.TryGetProperty("info_cache", out var infoCache))
            {
                foreach (var profile in infoCache.EnumerateObject())
                {
                    string shortcutName = profile.Value.GetProperty("shortcut_name").GetString()
                                        ?? profile.Value.GetProperty("name").GetString();
                    var iconPath = GetProfileIconPath(profile.Name);
                    profiles.Add(new Profile { Name = shortcutName, Directory = profile.Name, IcoPath = iconPath });
                }
            }
            return profiles;
        }

        /// <summary>
        /// Opens or switches to the specified Edge profile.
        /// </summary>
        /// <param name="profileName">The name of the profile.</param>
        /// <param name="profileDir">The directory of the profile.</param>
        private void OpenOrSwitchProfile(string profileName, string profileDir)
        {
            IntPtr hWnd = WindowHelper.FindWindow(profileName);

            if (hWnd != IntPtr.Zero)
            {
                WindowHelper.RestoreOrMaximizeWindow(hWnd);
            }
            else
            {
                StartEdgeProcess(profileDir);
            }
        }

        /// <summary>
        /// Starts the Edge process with the specified profile directory.
        /// </summary>
        /// <param name="profileDir">The directory of the profile.</param>
        private void StartEdgeProcess(string profileDir)
        {
            try
            {
                string edgeExePath = GetEdgeExePath();
                if (string.IsNullOrEmpty(edgeExePath))
                {
                    _context.API.ShowMsg("Error", "Microsoft Edge executable not found.");
                    return;
                }

                Process.Start(new ProcessStartInfo(edgeExePath, $"--profile-directory=\"{profileDir}\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _context.API.ShowMsg("Error", $"Failed to start Edge process: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the path to the profile icon.
        /// </summary>
        /// <param name="profileDir">The directory of the profile.</param>
        /// <returns>The path to the profile icon.</returns>
        private string GetProfileIconPath(string profileDir)
        {
            var iconPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Edge", "User Data", profileDir, "Edge Profile Picture.png");

            return File.Exists(iconPath) ? iconPath : DefaultIconPath;
        }

        /// <summary>
        /// Gets the path to the Edge executable.
        /// </summary>
        /// <returns>The path to the Edge executable if found; otherwise, null.</returns>
        private string GetEdgeExePath()
        {
            foreach (var path in EdgeExePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        /// <summary>
        /// Reloads the data asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ReloadDataAsync()
        {
            await UpdateProfilesCache();
        }
    }
}