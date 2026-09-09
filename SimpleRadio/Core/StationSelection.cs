using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Colossal;
using Colossal.IO.AssetDatabase;
using Colossal.Json;
using Colossal.PSI.Environment;
using Colossal.Serialization.Entities;
using Game;
using Game.Audio;
using Game.Audio.Radio;
using Game.SceneFlow;
using HarmonyLib;
using SimpleRadio.Settings;

namespace SimpleRadio.Core
{
    /// <summary>
    /// 管理城市載入、電台刷新與跨城市的選擇記憶。
    /// </summary>
    internal static class StationSelection
    {
        internal sealed class LoadState
        {
            internal Radio Radio;
            internal LoadState Parent;
            internal int Generation;
            internal bool Enable;
            internal bool UseCitySave;
            internal string PreferredStation;
            internal bool InjectionSucceeded;
            internal bool Failed;
            internal bool Completed;
            internal bool Succeeded;
        }

        private static readonly FieldInfo s_savedChannelField = AccessTools.Field(typeof(Radio), "m_LastSaveRadioChannel");
        private static SimpleRadioSettings s_settings;
        private static GameManager s_gameManager;
        private static string s_settingsPath;
        private static bool s_cityReady;
        private static bool s_loadingCity;
        private static string s_requestedStation;
        private static int s_generation;
        private static LoadState s_activeLoad;
        private static LoadState s_completedLoad;
        private static int s_refreshDepth;
        private static bool s_refreshFailed;
        private static bool s_saveRunning;
        private static bool s_saveDirty;
        private static int s_memoryGeneration;

        internal static bool CanRefresh => CanRecord(StationLoader.RadioInstance);

        internal static void Initialize(SimpleRadioSettings settings)
        {
            Dispose();
            s_memoryGeneration++;
            s_settings = settings;
            s_settingsPath = Path.Combine(EnvPath.kUserDataPath, "ModsSettings", Mod.ModName, Mod.ModName + SettingAsset.kExtension);
            s_gameManager = GameManager.instance;
            s_cityReady = s_gameManager.gameMode == GameMode.Game && !s_gameManager.isGameLoading;
            s_loadingCity = s_gameManager.gameMode == GameMode.Game && s_gameManager.isGameLoading;
            s_requestedStation = settings.RestoreLastStation ? settings.LastStation : null;
            s_gameManager.onGamePreload += OnGamePreload;
            s_gameManager.onGameLoadingComplete += OnGameLoadingComplete;
        }

        internal static void Dispose()
        {
            if (s_gameManager != null)
            {
                s_gameManager.onGamePreload -= OnGamePreload;
                s_gameManager.onGameLoadingComplete -= OnGameLoadingComplete;
            }

            s_generation++;
            s_settings = null;
            s_gameManager = null;
            s_cityReady = false;
            s_loadingCity = false;
            s_requestedStation = null;
            s_activeLoad = null;
            s_completedLoad = null;
            s_refreshDepth = 0;
            s_refreshFailed = false;
            s_saveRunning = false;
            s_saveDirty = false;
        }

        private static void OnGamePreload(Purpose purpose, GameMode mode)
        {
            s_generation++;
            s_cityReady = false;
            s_loadingCity = mode == GameMode.Game && (purpose == Purpose.LoadGame || purpose == Purpose.NewGame);
            s_requestedStation = s_settings.RestoreLastStation ? s_settings.LastStation : null;
            s_activeLoad = null;
            s_completedLoad = null;
            s_refreshDepth = 0;
            s_refreshFailed = false;
        }

        private static void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            s_cityReady = mode == GameMode.Game && (purpose == Purpose.LoadGame || purpose == Purpose.NewGame);
            s_loadingCity = false;
            if (s_cityReady && s_completedLoad?.Succeeded == true)
            {
                // 此事件觸發時 GameManager 仍為 Loading，不能經由 CanRecord 判定。
                TryRemember(s_completedLoad.Radio);
            }
            s_completedLoad = null;
        }

        internal static LoadState BeginLoad(Radio radio, bool enable)
        {
            if (s_settings == null || !IsCurrentRadio(radio) || (!s_cityReady && !s_loadingCity)) return null;

            bool useCitySave = s_loadingCity && s_refreshDepth == 0;
            var state = new LoadState
            {
                Radio = radio,
                Parent = s_activeLoad,
                Generation = s_generation,
                Enable = enable,
                UseCitySave = useCitySave,
                PreferredStation = useCitySave ? s_requestedStation : radio.currentChannel?.name
            };
            s_activeLoad = state;
            return state;
        }

        internal static void CompleteLoad(LoadState state, Exception exception)
        {
            if (state == null || state.Completed || state.Generation != s_generation) return;
            state.Completed = true;
            state.Failed |= exception != null || !state.InjectionSucceeded;

            try
            {
                // 巢狀載入共用最外層的恢復目標，內層只回報失敗。
                if (state.Parent != null)
                {
                    state.Parent.Failed |= state.Failed;
                }
                else if (state.Enable)
                {
                    bool validChannel = Restore(state);
                    state.Succeeded = validChannel && !state.Failed;
                }
            }
            catch (Exception e)
            {
                state.Failed = true;
                Mod.Logger.Warn(e, "電台恢復失敗，保留先前的電台記憶。");
            }
            finally
            {
                s_activeLoad = state.Parent;
                if (state.Parent == null) s_completedLoad = state;
            }

            if (state.Succeeded && CanRecord(state.Radio)) TryRemember(state.Radio);
        }

        internal static void BeginRefresh()
        {
            if (s_refreshDepth++ == 0)
            {
                s_completedLoad = null;
                s_refreshFailed = false;
            }
        }

        internal static bool EndRefresh(bool completed)
        {
            s_refreshFailed |= !completed;
            s_refreshDepth--;
            bool succeeded = !s_refreshFailed && s_completedLoad?.Succeeded == true;
            if (succeeded && CanRecord(s_completedLoad.Radio)) TryRemember(s_completedLoad.Radio);
            return succeeded;
        }

        internal static void OnChannelChanged(Radio radio)
        {
            if (CanRecord(radio)) TryRemember(radio);
        }

        private static bool IsCurrentRadio(Radio radio)
        {
            return radio != null && ReferenceEquals(radio, AudioManager.instance?.radio);
        }

        private static bool CanRecord(Radio radio)
        {
            return s_settings != null && s_cityReady && s_activeLoad == null && s_refreshDepth == 0
                && s_gameManager.gameMode == GameMode.Game && !s_gameManager.isGameLoading && IsCurrentRadio(radio);
        }

        private static bool HasValidChannel(Radio radio)
        {
            var channel = radio.currentChannel;
            return channel != null && !string.IsNullOrEmpty(channel.name)
                && ReferenceEquals(channel, radio.GetRadioChannel(channel.name));
        }

        private static bool Restore(LoadState state)
        {
            Radio radio = state.Radio;
            string target = state.PreferredStation;
            var channel = string.IsNullOrEmpty(target) ? null : radio.GetRadioChannel(target);
            if (channel == null && state.UseCitySave)
            {
                if (s_savedChannelField == null) throw new MissingFieldException(typeof(Radio).FullName, "m_LastSaveRadioChannel");
                target = (string)s_savedChannelField.GetValue(radio);
                channel = string.IsNullOrEmpty(target) ? null : radio.GetRadioChannel(target);
            }

            if (channel != null && !ReferenceEquals(radio.currentChannel, channel))
            {
                radio.currentChannel = channel;
                Mod.Logger.Info($"已恢復電台: {target}");
            }
            return HasValidChannel(radio);
        }

        private static void TryRemember(Radio radio)
        {
            try
            {
                Remember(radio);
            }
            catch (Exception e)
            {
                Mod.Logger.Warn(e, "記錄電台選擇失敗，保留先前記憶。");
            }
        }

        private static void Remember(Radio radio)
        {
            if (s_settings == null || !IsCurrentRadio(radio) || !HasValidChannel(radio)) return;
            string name = radio.currentChannel.name;
            if (s_settings.LastStation != name)
            {
                s_settings.LastStation = name;
                s_saveDirty = true;
            }

            if (s_saveDirty && !s_saveRunning)
            {
                s_saveRunning = true;
                var settings = s_settings;
                string path = s_settingsPath;
                int generation = s_memoryGeneration;
                // 正常退出會等待 TaskManager 的佇列；已排隊的保存可在 OnDispose 後完成。
                _ = TaskManager.instance.EnqueueTask("SimpleRadio.StationMemory",
                    () => SaveMemoryAsync(settings, path, generation));
            }
        }

        private static async Task SaveMemoryAsync(SimpleRadioSettings settings, string path, int generation)
        {
            try
            {
                while (generation == s_memoryGeneration)
                {
                    // 原版佇列 cap 為 2，忙碌時的新請求可能被略過；先等既有工作結束。
                    await TaskManager.instance.taskQueue.Complete("SaveSettings");
                    if (generation != s_memoryGeneration) return;
                    string requested = settings.LastStation;
                    await AssetDatabase.global.SaveSpecificSetting(nameof(SimpleRadioSettings));
                    await TaskManager.instance.taskQueue.Complete("SaveSettings");
                    if (generation != s_memoryGeneration) return;

                    // TaskQueue 會吞掉寫入例外，必須核對磁碟內容才能清除 dirty。
                    string persisted = await Task.Run(() => ReadPersistedStation(path));
                    if (generation != s_memoryGeneration) return;
                    if (persisted == settings.LastStation)
                    {
                        if (ReferenceEquals(settings, s_settings)) s_saveDirty = false;
                        return;
                    }
                    if (requested == settings.LastStation)
                    {
                        Mod.Logger.Warn("電台記憶尚未寫入設定檔，將於下次切台或載入後重試。");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                if (generation == s_memoryGeneration)
                {
                    Mod.Logger.Warn(e, "保存電台記憶失敗，將於下次切台或載入後重試。");
                }
            }
            finally
            {
                if (ReferenceEquals(settings, s_settings)) s_saveRunning = false;
            }
        }

        private static string ReadPersistedStation(string path)
        {
            string text = File.ReadAllText(path);
            var blocks = new COCParser().Parse(text, out var report);
            if (report.Count != 0) throw new InvalidDataException("SimpleRadio 設定檔解析失敗。");
            if (!blocks.TryGetValue(Mod.ModName, out var block)) return string.Empty;

            var settings = JSON.Load(text.Substring(block.startIndex, block.length));
            return settings.TryGetValue(nameof(SimpleRadioSettings.LastStation), out var station)
                ? (string)station ?? string.Empty
                : string.Empty;
        }
    }
}
