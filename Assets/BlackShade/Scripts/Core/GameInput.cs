using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BlackShade
{
    // 키보드/마우스 입력을 한 곳에서 읽는 도우미.
    // 프로젝트가 새 Input System을 쓰든 예전 Input Manager를 쓰든 똑같이 동작해요.
    //
    // 조작
    //   WASD 이동 / Shift 달리기 / 마우스 시점 / E 상호작용
    //   F 손전등 / Tab(또는 I) 메뉴 / 마우스 왼쪽 빈 캔 던지기 / ESC 일시정지
    public static class GameInput
    {
        // ------------------------------------------------------------ 범용
        public static bool Key(KeyCode k)
        {
#if ENABLE_INPUT_SYSTEM
            var c = Control(k);
            return c != null && c.isPressed;
#else
            return Input.GetKey(k);
#endif
        }

        public static bool KeyDown(KeyCode k)
        {
#if ENABLE_INPUT_SYSTEM
            var c = Control(k);
            return c != null && c.wasPressedThisFrame;
#else
            return Input.GetKeyDown(k);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Controls.KeyControl Control(KeyCode k)
        {
            var kb = Keyboard.current;
            if (kb == null) return null;
            if (k >= KeyCode.A && k <= KeyCode.Z)
            {
                var key = (UnityEngine.InputSystem.Key)System.Enum.Parse(typeof(UnityEngine.InputSystem.Key), k.ToString());
                return kb[key];
            }
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return kb[(UnityEngine.InputSystem.Key)((int)UnityEngine.InputSystem.Key.Digit0 + (k - KeyCode.Alpha0))];
            switch (k)
            {
                case KeyCode.Space: return kb.spaceKey;
                case KeyCode.Return: return kb.enterKey;
                case KeyCode.KeypadEnter: return kb.numpadEnterKey;
                case KeyCode.Escape: return kb.escapeKey;
                case KeyCode.Tab: return kb.tabKey;
                case KeyCode.LeftShift: return kb.leftShiftKey;
                case KeyCode.UpArrow: return kb.upArrowKey;
                case KeyCode.DownArrow: return kb.downArrowKey;
                case KeyCode.LeftArrow: return kb.leftArrowKey;
                case KeyCode.RightArrow: return kb.rightArrowKey;
                case KeyCode.Backspace: return kb.backspaceKey;
                case KeyCode.F1: return kb.f1Key;
                case KeyCode.F2: return kb.f2Key;
                case KeyCode.F3: return kb.f3Key;
                case KeyCode.F4: return kb.f4Key;
                case KeyCode.F5: return kb.f5Key;
                case KeyCode.F6: return kb.f6Key;
                case KeyCode.F8: return kb.f8Key;
                case KeyCode.F9: return kb.f9Key;
            }
            return null;
        }
#endif

        public static bool MouseDown(int button)
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m == null) return false;
            return button == 0 ? m.leftButton.wasPressedThisFrame : button == 1 ? m.rightButton.wasPressedThisFrame : m.middleButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(button);
#endif
        }

        public static float Scroll()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m == null ? 0f : m.scroll.ReadValue().y / 120f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

        // ------------------------------------------------------------ 이동 / 시점
        public static Vector2 Move()
        {
            float x = 0f, y = 0f;
            if (Key(KeyCode.W) || Key(KeyCode.UpArrow)) y += 1f;
            if (Key(KeyCode.S) || Key(KeyCode.DownArrow)) y -= 1f;
            if (Key(KeyCode.D) || Key(KeyCode.RightArrow)) x += 1f;
            if (Key(KeyCode.A) || Key(KeyCode.LeftArrow)) x -= 1f;
            Vector2 v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        // 마우스 이동량 (대략 '도' 단위로 맞춘 값)
        public static Vector2 Look()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m == null) return Vector2.zero;
            return m.delta.ReadValue() * 0.1f;
#else
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
        }

        public static bool Run() { return Key(KeyCode.LeftShift); }

        // ------------------------------------------------------------ 게임 조작
        public static bool InteractDown() { return KeyDown(KeyCode.E); }
        public static bool InteractHeld() { return Key(KeyCode.E); }
        public static bool FlashlightDown() { return KeyDown(KeyCode.F); }
        public static bool MenuDown() { return KeyDown(KeyCode.Tab) || KeyDown(KeyCode.I); }
        public static bool ThrowDown() { return MouseDown(0); }
        public static bool EscapeDown() { return KeyDown(KeyCode.Escape); }
        public static bool MouseClickDown() { return MouseDown(0); }

        // 대사 넘기기: E, 스페이스, 엔터, 마우스 왼쪽
        public static bool AdvanceDown()
        {
            return KeyDown(KeyCode.E) || KeyDown(KeyCode.Space) || KeyDown(KeyCode.Return) || MouseDown(0);
        }

        // 창 닫기
        public static bool CloseDown()
        {
            return KeyDown(KeyCode.E) || KeyDown(KeyCode.Escape) || KeyDown(KeyCode.Space);
        }

        // 팝업에서 선택/취소/이동
        public static bool ConfirmDown() { return KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter) || KeyDown(KeyCode.Space) || KeyDown(KeyCode.E); }
        public static bool CancelDown() { return KeyDown(KeyCode.Escape) || KeyDown(KeyCode.Backspace); }
        public static bool UpDown() { return KeyDown(KeyCode.W) || KeyDown(KeyCode.UpArrow); }
        public static bool DownDown() { return KeyDown(KeyCode.S) || KeyDown(KeyCode.DownArrow); }
        public static bool LeftDown() { return KeyDown(KeyCode.A) || KeyDown(KeyCode.LeftArrow); }
        public static bool RightDown() { return KeyDown(KeyCode.D) || KeyDown(KeyCode.RightArrow); }
        public static bool TabPrevDown() { return KeyDown(KeyCode.Q); }
        public static bool TabNextDown() { return KeyDown(KeyCode.R); }

        // 숫자 키 (0~9), 없으면 -1
        public static int DigitDown()
        {
            for (int i = 0; i <= 9; i++)
                if (KeyDown(KeyCode.Alpha0 + i)) return i;
            return -1;
        }

        // 개발용: F8 프롤로그 '무서운 얘기'로 건너뛰기 / F9 타이틀에서 본편 바로 시작
        public static bool DebugSkipDown() { return KeyDown(KeyCode.F8); }
        public static bool DebugMainDown() { return KeyDown(KeyCode.F9); }
    }
}
