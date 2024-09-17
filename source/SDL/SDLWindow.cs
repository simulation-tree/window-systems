using System;
using System.Diagnostics;
using System.Numerics;
using Unmanaged;
using static SDL3.SDL3;

namespace SDL3
{
    public unsafe struct SDLWindow : IDisposable
    {
        private readonly SDL_Window window;
        private float x;
        private float y;
        private float width;
        private float height;

        public readonly bool IsDestroyed => window.IsNull;
        public readonly uint ID => (uint)SDL_GetWindowID(window);
        public readonly SDL_WindowFlags Flags => SDL_GetWindowFlags(window);

        public readonly string Title
        {
            get => SDL_GetWindowTitle(window) ?? "";
            set => SDL_SetWindowTitle(window, value);
        }

        public Vector2 Position
        {
            readonly get => new(x, y);
            set
            {
                x = value.X;
                y = value.Y;
                SDL_SetWindowPosition(window, (int)x, (int)y);
            }
        }

        public Vector2 Size
        {
            readonly get => new(width, height);
            set
            {
                width = value.X;
                height = value.Y;
                SDL_SetWindowSize(window, (int)width, (int)height);
            }
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
            set => SDL_SetWindowBordered(window, !value);
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

        public readonly bool IsAlwaysOnTop
        {
            get => (SDL_GetWindowFlags(window) & SDL_WindowFlags.AlwaysOnTop) == SDL_WindowFlags.AlwaysOnTop;
            set => SDL_SetWindowAlwaysOnTop(window, value);
        }

        public readonly bool LockCursor
        {
            get => SDL_GetWindowMouseGrab(window);
            set => SDL_SetWindowMouseGrab(window, value);
        }

        public readonly bool IsMaximized => (SDL_GetWindowFlags(window) & SDL_WindowFlags.Maximized) == SDL_WindowFlags.Maximized;

        public readonly SDLDisplay Display
        {
            get
            {
                SDL_DisplayID displayId = SDL_GetDisplayForWindow(window);
                SDL_DisplayMode* displayMode = SDL_GetCurrentDisplayMode(displayId);
                return new(displayId, displayMode);
            }
        }

        internal SDLWindow(SDL_Window existingWindow)
        {
            window = existingWindow;
        }

        public SDLWindow(USpan<char> title, Vector2 size, SDL_WindowFlags flags)
        {
            width = size.X;
            height = size.Y;
            window = SDL_CreateWindow(title.ToString(), (int)width, (int)height, flags);
            SDL_GetWindowPosition(window, out int x, out int y);
            this.x = x;
            this.y = y;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDestroyed)
            {
                throw new ObjectDisposedException(nameof(SDLWindow));
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
        public void Center()
        {
            ThrowIfDisposed();
            SDL_SetWindowPosition(window, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED);
            SDL_GetWindowPosition(window, out int x, out int y);
            this.x = x;
            this.y = y;
        }

        public readonly (int x, int y) GetRealPosition()
        {
            ThrowIfDisposed();
            SDL_GetWindowPosition(window, out int x, out int y);
            return (x, y);
        }

        public readonly (int width, int height) GetRealSize()
        {
            ThrowIfDisposed();
            SDL_GetWindowSize(window, out int width, out int height);
            return (width, height);
        }

        public readonly nint CreateVulkanSurface(nint vulkanInstance)
        {
            ThrowIfDisposed();
            ulong* surfacePointer = stackalloc ulong[1];
            nint allocator = 0;
            int result = SDL_Vulkan_CreateSurface(window, vulkanInstance, allocator, &surfacePointer);
            if (result != 0)
            {
                throw new Exception($"Could not create surface: {SDL_GetError()}");
            }

            if ((nint)surfacePointer == 0)
            {
                throw new Exception("Unable to correctly create a surface using the window's flags");
            }

            return (nint)surfacePointer;
        }

        public readonly void SetTransparency(float alpha)
        {
            SDL_PixelFormat format = SDL_GetWindowPixelFormat(window);
            (int width, int height) = GetRealSize();
            SDL_Surface* shape = SDL_CreateSurface(width, height, format);
            SDL_ClearSurface(shape, 0f, 0f, 0f, alpha);
            int result = SDL_SetWindowShape(window, shape);
            SDL_DestroySurface(shape);
        }
    }
}
