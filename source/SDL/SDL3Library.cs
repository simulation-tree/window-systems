using System;
using System.Diagnostics;
using Unmanaged.Collections;
using static SDL.SDL;

namespace SDL
{
    public unsafe readonly struct SDL3Library : IDisposable
    {
        public readonly SDL_version version;

        private readonly UnmanagedArray<char> platform;

        public readonly bool IsDisposed => platform.IsDisposed;
        public readonly ReadOnlySpan<char> Platform => platform.AsSpan();

        public SDL3Library() : this(true, true)
        {
        }

        [Conditional("DEBUG")]
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(SDL3Library));
            }
        }

        public SDL3Library(bool video = true, bool audio = true)
        {
            SDL_InitFlags flags = SDL_InitFlags.Timer | SDL_InitFlags.Gamepad | SDL_InitFlags.Events;
            if (video)
            {
                flags |= SDL_InitFlags.Video;
            }

            if (audio)
            {
                flags |= SDL_InitFlags.Audio;
            }

            if (SDL_Init(flags) != 0)
            {
                throw new Exception($"Failed to initialize SDL library: {SDL_GetErrorString()}");
            }

            SDL_GetVersion(out version);
            platform = new(SDL_GetPlatformString().AsSpan());
            SDL_SetLogOutputFunction(LogSDLOutput);
        }

        public readonly void Dispose()
        {
            ThrowIfDisposed();
            platform.Dispose();
            SDL_Quit();
        }

        private void LogSDLOutput(SDL_LogCategory category, SDL_LogPriority priority, string message)
        {
            ThrowIfDisposed();
            if (priority >= SDL_LogPriority.Error)
            {
                throw new Exception($"{priority} [{category}]: {message}");
            }

            Debug.WriteLine($"{priority} [{category}]: {message}");
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
            return SDL_IsGamepad(id) == SDL_bool.SDL_TRUE;
        }

        public readonly SDL_Gamepad OpenGamepad(SDL_JoystickID id)
        {
            ThrowIfDisposed();
            return SDL_OpenGamepad(id);
        }

        public readonly SDL_GamepadType GetGamepadInstanceType(SDL_JoystickID id)
        {
            ThrowIfDisposed();
            return SDL_GetGamepadInstanceType(id);
        }

        public readonly SDL_Gamepad GetGamepadFromInstanceID(SDL_JoystickID id)
        {
            ThrowIfDisposed();
            return SDL_GetGamepadFromInstanceID(id);
        }

        public readonly bool HasKeyboard()
        {
            ThrowIfDisposed();
            return SDL_HasKeyboard() == SDL_bool.SDL_TRUE;
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

        public readonly SDL_Cursor* GetCursor()
        {
            ThrowIfDisposed();
            return SDL_GetCursor();
        }

        public readonly SDL_Cursor* CreateSystemCursor(SDL_SystemCursor id)
        {
            ThrowIfDisposed();
            return SDL_CreateSystemCursor(id);
        }

        public readonly SDL_Cursor* GetDefaultCursor()
        {
            ThrowIfDisposed();
            return SDL_GetDefaultCursor();
        }

        public readonly void DestroyCursor(SDL_Cursor* cursor)
        {
            ThrowIfDisposed();
            SDL_DestroyCursor(cursor);
        }

        public readonly void SetCursor(SDL_Cursor* cursor)
        {
            ThrowIfDisposed();
            SDL_SetCursor(cursor);
        }

        public readonly void SetHint(ReadOnlySpan<byte> name, bool value)
        {
            ThrowIfDisposed();
            SDL_SetHint(name, value);
        }

        public readonly string[] GetVulkanInstanceExtensions()
        {
            ThrowIfDisposed();
            return SDL_Vulkan_GetInstanceExtensions();
        }

        public readonly SDL3Window GetWindow(uint windowId)
        {
            ThrowIfDisposed();
            SDL_Window window = SDL_GetWindowFromID(windowId);
            return new(window);
        }
    }
}
