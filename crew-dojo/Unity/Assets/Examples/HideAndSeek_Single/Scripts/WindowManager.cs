using UnityEngine;
using System;
using System.Runtime.InteropServices;


namespace Examples.HideAndSeek_Single
{

    public class WindowManager : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN
        [DllImport("user32.dll", EntryPoint="FindWindow", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", EntryPoint="SetWindowPos")]
        private static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);


        private void Start()
        {
            Invoke(nameof(SetPositionAndSize), 0.1f);
        }

        private void SetPositionAndSize()
        {
            //Name of the window (You can get this from your build settings)
            string windowName = Application.productName;
            // Get the command line arguments (including the executable)
            string[] args = Environment.GetCommandLineArgs();

            // Find the position argument (assuming it's the last argument and an integer)
            // This should be provided when launching the app (e.g., YourApp.exe 0)
            int position = 0;
            if (args.Length > 1 && int.TryParse(args[args.Length - 1], out int pos))
            {
                position = pos;
            }

            // Screen resolution
            int screenWidth = Screen.currentResolution.width;
            int screenHeight = Screen.currentResolution.height;

            // Window size should be 1/2 of screen width and 1/3 of screen height
            int windowWidth = screenWidth / 3;
            int windowHeight = screenHeight / 2;

            // Calculate window position based on its position in the grid
            int col = position % 3; // Column (0 or 1) based on position
            int row = position / 3; // Row (0, 1, or 2) based on position

            int windowX = col * windowWidth;
            int windowY = row * windowHeight;

            // Adjust the y-coordinate for the window from top-origin to bottom-origin
            windowY = screenHeight - (windowY + windowHeight);

            // The FindWindow and SetWindowPos code goes here, using the calculated windowX, windowY, windowWidth, and windowHeight

            IntPtr hwnd = FindWindow(null, windowName);

            if (hwnd == IntPtr.Zero)
            {
                Debug.LogError("Window not found!");
                return;
            }

            IntPtr topMost = new IntPtr(-1);
            const int SWP_NOZORDER = 0x0004; // Do not change Z order
            var success = SetWindowPos(hwnd, topMost, windowX, windowY, windowWidth, windowHeight, SWP_NOZORDER);
            Debug.Log("SetWindowPos success: " + success);
        }


#endif
    }
}
