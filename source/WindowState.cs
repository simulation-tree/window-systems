using Windows.Components;

namespace Windows.Systems
{
    public struct WindowState
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public IsWindow.State state;
        public IsWindow.Flags flags;

        public WindowState(int x, int y, int width, int height, IsWindow.State state, IsWindow.Flags flags)
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
