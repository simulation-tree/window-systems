using System;
using System.Diagnostics;
using Unmanaged;
using static SDL3.SDL3;

namespace SDL3
{
    public unsafe readonly struct Library : IDisposable
    {
        public readonly int version;

        private readonly Text platform;

        public readonly bool IsDisposed => platform.IsDisposed;
        public readonly USpan<char> Platform => platform.AsSpan();

        /// <summary>
        /// Checks if the cursor is visible.
        /// </summary>
        public readonly bool IsCursorVisible => SDL_CursorVisible();

        public Library() : this(true, true)
        {
        }

        public Library(bool video = true, bool audio = true)
        {
            SDL_InitFlags flags = SDL_InitFlags.Gamepad | SDL_InitFlags.Events;
            if (video)
            {
                flags |= SDL_InitFlags.Video;
            }

            if (audio)
            {
                flags |= SDL_InitFlags.Audio;
            }

            if (!SDL_Init(flags))
            {
                throw new Exception($"Failed to initialize SDL library: {SDL_GetError()}");
            }

            version = SDL_GetVersion();
            platform = new(SDL_GetPlatform() ?? "unknown");
            SDL_SetLogOutputFunction(LogOutput);
        }


        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Library));
            }
        }

        public readonly void Dispose()
        {
            ThrowIfDisposed();

            platform.Dispose();
            SDL_Quit();
        }

        private void LogOutput(SDL_LogCategory category, SDL_LogPriority priority, string? message)
        {
            ThrowIfDisposed();

            if (priority >= SDL_LogPriority.Error)
            {
                throw new Exception($"{priority} [{category}]: {message}");
            }

            Trace.WriteLine($"{priority} [{category}]: {message}");
        }

        public bool PollEvent(out SDL_Event message)
        {
            ThrowIfDisposed();

            SDL_Event m;
            bool f = SDL_PollEvent(&m);
            message = m;
            return f;
        }

        public readonly void CloseGamepad(SDL_Gamepad gamepad)
        {
            ThrowIfDisposed();

            SDL_CloseGamepad(gamepad);
        }

        public readonly bool IsGamepad(SDL_JoystickID id)
        {
            ThrowIfDisposed();

            return SDL_IsGamepad(id);
        }

        public readonly SDL_Gamepad OpenGamepad(SDL_JoystickID id)
        {
            ThrowIfDisposed();

            return SDL_OpenGamepad(id);
        }

        public readonly SDL_GamepadType GetGamepadInstanceType(SDL_JoystickID id)
        {
            ThrowIfDisposed();

            return SDL_GetGamepadTypeForID(id);
        }

        public readonly SDL_Gamepad GetGamepadFromInstanceID(SDL_JoystickID id)
        {
            ThrowIfDisposed();

            return SDL_GetGamepadFromID(id);
        }

        public readonly bool HasKeyboard()
        {
            ThrowIfDisposed();

            return SDL_HasKeyboard();
        }

        public readonly void ShowCursor()
        {
            ThrowIfDisposed();

            SDL_ShowCursor();
        }

        public readonly void HideCursor()
        {
            ThrowIfDisposed();

            SDL_HideCursor();
        }

        public readonly SDL_Cursor GetCursor()
        {
            ThrowIfDisposed();

            return SDL_GetCursor();
        }

        public readonly SDL_Cursor CreateSystemCursor(SDL_SystemCursor id)
        {
            ThrowIfDisposed();

            return SDL_CreateSystemCursor(id);
        }

        public readonly SDL_Cursor GetDefaultCursor()
        {
            ThrowIfDisposed();

            return SDL_GetDefaultCursor();
        }

        public readonly void DestroyCursor(SDL_Cursor cursor)
        {
            ThrowIfDisposed();

            SDL_DestroyCursor(cursor);
        }

        public readonly void SetCursor(SDL_Cursor cursor)
        {
            ThrowIfDisposed();

            SDL_SetCursor(cursor);
        }

        public readonly void SetHint(USpan<byte> name, bool value)
        {
            ThrowIfDisposed();

            SDL_SetHint(name, value);
        }

        public readonly FixedString[] GetVulkanInstanceExtensions()
        {
            ThrowIfDisposed();

            string[] extensionNames = SDL_Vulkan_GetInstanceExtensions();
            USpan<FixedString> extensions = stackalloc FixedString[extensionNames.Length];
            for (uint i = 0; i < extensionNames.Length; i++)
            {
                extensions[i] = extensionNames[i];
            }

            return extensions.ToArray();
        }

        public readonly SDLWindow GetWindow(uint windowId)
        {
            ThrowIfDisposed();

            SDL_Window window = SDL_GetWindowFromID((SDL_WindowID)windowId);
            return new(window);
        }
    }
}
