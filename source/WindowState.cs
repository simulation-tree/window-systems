namespace Windows.Systems
{
    public struct SDLWindowState
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public WindowState state;
        public WindowFlags flags;

        public SDLWindowState(int x, int y, int width, int height, WindowState state, WindowFlags flags)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.state = state;
            this.flags = flags;
        }
    }
}
