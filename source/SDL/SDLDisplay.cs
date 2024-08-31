namespace SDL3
{
    public unsafe readonly struct SDLDisplay
    {
        private readonly SDL_DisplayID displayId;
        private readonly SDL_DisplayMode* displayMode;

        public readonly uint ID => (uint)displayId;
        public readonly uint Width => (uint)displayMode->w;
        public readonly uint Height => (uint)displayMode->h;
        public readonly uint RefreshRate => (uint)displayMode->refresh_rate;

        internal SDLDisplay(SDL_DisplayID displayId, SDL_DisplayMode* displayMode)
        {
            this.displayId = displayId;
            this.displayMode = displayMode;
        }
    }
}
