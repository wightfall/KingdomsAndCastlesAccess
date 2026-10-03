using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// Replaces the game's mouse pointer. While the keyboard cursor is in use, every ray the game casts
    /// points straight down at the cursor tile and "clicks" are generated from key presses, so the game's
    /// own placement, drag-building and cursor-mode code works unchanged. Moving the mouse hands control back.
    /// </summary>
    internal sealed class VirtualPointer : IPointer
    {
        private readonly IPointer mouse;
        private int downFrame = -10;
        private int upFrame = -10;
        private bool held;
        private Vector3 lastMousePos;

        internal static VirtualPointer Inst { get; private set; }

        /// <summary>True while the keyboard cursor drives the pointer.</summary>
        internal bool KeyboardActive { get; set; }

        /// <summary>World position (cell centre) the keyboard cursor points at.</summary>
        internal Vector3 Target { get; set; }

        private VirtualPointer(IPointer mouse)
        {
            this.mouse = mouse;
            lastMousePos = Input.mousePosition;
        }

        /// <summary>Installs the pointer (once the game created its own).</summary>
        internal static void Install()
        {
            var current = PointingSystem.GetPointer();
            if (current == null || current is VirtualPointer) return;
            Inst = new VirtualPointer(current);
            PointingSystem.ReplacePointer(Inst);
        }

        /// <summary>Mouse movement or clicks give control back to the mouse.</summary>
        internal void Tick()
        {
            Vector3 m = Input.mousePosition;
            if ((m - lastMousePos).sqrMagnitude > 9f || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (KeyboardActive && !held) KeyboardActive = false;
            }
            lastMousePos = m;
        }

        /// <summary>Simulate a full click: down on the next frame, up on the one after.</summary>
        internal void Click()
        {
            KeyboardActive = true;
            downFrame = Time.frameCount + 1;
            upFrame = Time.frameCount + 2;
            held = false;
        }

        /// <summary>Press and keep the button down (start of a drag).</summary>
        internal void Press()
        {
            KeyboardActive = true;
            downFrame = Time.frameCount + 1;
            upFrame = -10;
            held = true;
        }

        /// <summary>Release a held button (end of a drag).</summary>
        internal void Release()
        {
            if (!held) return;
            held = false;
            upFrame = Time.frameCount + 1;
        }

        internal bool IsHeld => held;

        public Ray GetRay()
        {
            if (!KeyboardActive) return mouse.GetRay();
            return new Ray(Target + new Vector3(0f, 40f, 0f), Vector3.down);
        }

        public bool GetPrimaryDown() => Time.frameCount == downFrame || (!KeyboardActive && mouse.GetPrimaryDown());

        public bool GetPrimaryDownThisFrame() => (held && Time.frameCount >= downFrame) || Time.frameCount == downFrame || (!KeyboardActive && mouse.GetPrimaryDownThisFrame());

        public bool GetPrimaryUp() => Time.frameCount == upFrame || (!KeyboardActive && mouse.GetPrimaryUp());

        public bool GetSecondaryDown() => !KeyboardActive && mouse.GetSecondaryDown();

        public bool GetSecondaryUp() => !KeyboardActive && mouse.GetSecondaryUp();

        public bool GetTertiaryDown() => !KeyboardActive && mouse.GetTertiaryDown();

        public bool GetTertiaryUp() => !KeyboardActive && mouse.GetTertiaryUp();

        public Vector3 GetScreenPos(Vector3 worldPos) => mouse.GetScreenPos(worldPos);
    }
}
