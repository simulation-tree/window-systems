using SDL3;
using System;
using System.Diagnostics;
using Unmanaged;
using Windows.Components;
using static SDL3.SDL3;

namespace SDL
{
    public unsafe readonly struct SDL3Window : IDisposable
    {
        private readonly SDL_Window window;

        public readonly bool IsDestroyed => window.IsNull;
        public readonly uint ID => (uint)SDL_GetWindowID(window);
        public readonly SDL_WindowFlags Flags => SDL_GetWindowFlags(window);

        public readonly string Title
        {
            get => SDL_GetWindowTitle(window) ?? "";
            set => SDL_SetWindowTitle(window, value);
        }

        public readonly WindowPosition Position
        {
            get
            {
                SDL_GetWindowPosition(window, out int x, out int y);
                return new(x, y);
            }
            set => SDL_SetWindowPosition(window, value.x, value.y);
        }

        public readonly WindowSize Size
        {
            get
            {
                SDL_GetWindowSize(window, out int width, out int height);
                return new((uint)width, (uint)height);
            }
            set => SDL_SetWindowSize(window, (int)value.width, (int)value.height);
        }

        public readonly bool IsBorderless
        {
            get
            {
                int top = default;
                int left = default;
                int bottom = default;
                int right = default;
                SDL_GetWindowBordersSize(window, &top, &left, &bottom, &right);
                return top == 0 && left == 0 && bottom == 0 && right == 0;
            }
            set => SDL_SetWindowBordered(window, value);
        }

        public readonly bool IsResizable
        {
            get => (SDL_GetWindowFlags(window) & SDL_WindowFlags.Resizable) == SDL_WindowFlags.Resizable;
            set => SDL_SetWindowResizable(window, value);
        }

        public readonly bool IsFullscreen
        {
            get => (SDL_GetWindowFlags(window) & SDL_WindowFlags.Fullscreen) == SDL_WindowFlags.Fullscreen;
            set => SDL_SetWindowFullscreen(window, value);
        }

        public readonly bool IsMinimized
        {
            get => (SDL_GetWindowFlags(window) & SDL_WindowFlags.Minimized) == SDL_WindowFlags.Minimized;
            set
            {
                if (value)
                {
                    SDL_MinimizeWindow(window);
                }
                else
                {
                    SDL_RestoreWindow(window);
                }
            }
        }

        public readonly bool LockCursor
        {
            get => SDL_GetWindowMouseGrab(window);
            set => SDL_SetWindowMouseGrab(window, value);
        }

        public readonly bool IsMaximized => (SDL_GetWindowFlags(window) & SDL_WindowFlags.Maximized) == SDL_WindowFlags.Maximized;

        internal SDL3Window(SDL_Window existingWindow)
        {
            window = existingWindow;
        }

        public SDL3Window(ReadOnlySpan<char> title, uint width, uint height, SDL_WindowFlags flags)
        {
            window = SDL_CreateWindow(title.ToString(), (int)width, (int)height, flags);
        }

        [Conditional("DEBUG")]
        private void ThrowIfDisposed()
        {
            if (IsDestroyed)
            {
                throw new ObjectDisposedException(nameof(SDL3Window));
            }
        }

        /// <summary>
        /// Destroys and disposes the SDL window.
        /// </summary>
        public readonly void Dispose()
        {
            ThrowIfDisposed();
            SDL_DestroyWindow(window);
        }

        public readonly void Maximize()
        {
            ThrowIfDisposed();
            SDL_MaximizeWindow(window);
        }

        public readonly void Restore()
        {
            ThrowIfDisposed();
            SDL_RestoreWindow(window);
        }

        /// <summary>
        /// Centers the window to the middle of the screen.
        /// </summary>
        public readonly void Center()
        {
            ThrowIfDisposed();
            SDL_SetWindowPosition(window, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED);
        }

        public readonly nint CreateVulkanSurface(nint vulkanInstance)
        {
            ThrowIfDisposed();
            ulong surfacePointer;
            nint allocator = 0;
            if (SDL_Vulkan_CreateSurface(window, vulkanInstance, allocator, &surfacePointer) == 0)
            {
                throw new Exception("Could not create surface");
            }

            return (nint)surfacePointer;
        }

        /// <summary>
        /// Retrieves the names of the extensions required for surface creation.
        /// <para>
        /// These are used when creating Vulkan instances.
        /// </para>
        /// </summary>
        public static int GetVulkanExtensionNames(Span<FixedString> buffer)
        {
            string[] extensionNames = SDL_Vulkan_GetInstanceExtensions();
            for (int i = 0; i < extensionNames.Length; i++)
            {
                buffer[i] = extensionNames[i];
            }

            return extensionNames.Length;
        }
    }
}
