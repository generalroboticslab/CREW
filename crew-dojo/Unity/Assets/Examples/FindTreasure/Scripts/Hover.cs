using UnityEngine;
using Dojo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Tobii.StreamEngine;
// using Tobii.Gaming;

namespace Examples.FindTreasure
{
    public class Hover : MonoBehaviour
    {
        private DojoConnection _connection;
        private Rect applicationRect;


        private static void OnGazePoint(ref tobii_gaze_point_t gazePoint, IntPtr userData)
        {
            // Check that the data is valid before using it
            if(gazePoint.validity == tobii_validity_t.TOBII_VALIDITY_VALID)
            {
                Console.WriteLine($"Gaze point: {gazePoint.position.x}, {gazePoint.position.y}");
            }
        }

        private void Awake()
        {
            _connection = FindObjectOfType<DojoConnection>();
        }


        void Start()
        {

            _connection = FindObjectOfType<DojoConnection>();
            transform.Find("FramePanel").gameObject.SetActive(false);


            IntPtr apiContext;
            tobii_error_t result = Interop.tobii_api_create(out apiContext, null);
            System.Diagnostics.Debug.Assert(result == tobii_error_t.TOBII_ERROR_NO_ERROR);

            // Enumerate devices to find connected eye trackers
            List<string> urls;
            result = Interop.tobii_enumerate_local_device_urls(apiContext, out urls);
            System.Diagnostics.Debug.Assert(result == tobii_error_t.TOBII_ERROR_NO_ERROR);

            UnityEngine.Debug.Log("Found " + urls.Count + " device(s)");

            if(urls.Count == 0)
            {
                UnityEngine.Debug.Log("Error: No device found");
                return;
            }

            // Connect to the first tracker found
            IntPtr deviceContext;
            result = Interop.tobii_device_create(apiContext, urls[0], Interop.tobii_field_of_use_t.TOBII_FIELD_OF_USE_STORE_OR_TRANSFER_FALSE, out deviceContext);
            System.Diagnostics.Debug.Assert(result == tobii_error_t.TOBII_ERROR_NO_ERROR);

            // Subscribe to gaze data
            result = Interop.tobii_gaze_point_subscribe(deviceContext, OnGazePoint);
            System.Diagnostics.Debug.Assert(result == tobii_error_t.TOBII_ERROR_NO_ERROR);

            // This sample will collect 1000 gaze points
            for ( int i=0; i<1000; i++ )
            {
                // Optionally block this thread until data is available. Especially useful if running in a separate thread.
                Interop.tobii_wait_for_callbacks(new [] { deviceContext });
                System.Diagnostics.Debug.Assert(result == tobii_error_t.TOBII_ERROR_NO_ERROR || result == tobii_error_t.TOBII_ERROR_TIMED_OUT);

                // Process callbacks on this thread if data is available
                Interop.tobii_device_process_callbacks(deviceContext);
                System.Diagnostics.Debug.Assert(result == tobii_error_t.TOBII_ERROR_NO_ERROR);
            }

            // Cleanup
            result = Interop.tobii_gaze_point_unsubscribe(deviceContext);
            System.Diagnostics.Debug.Assert(result == tobii_error_t.TOBII_ERROR_NO_ERROR);
            result = Interop.tobii_device_destroy(deviceContext);
            System.Diagnostics.Debug.Assert(result == tobii_error_t.TOBII_ERROR_NO_ERROR);
            result = Interop.tobii_api_destroy(apiContext);
            System.Diagnostics.Debug.Assert(result == tobii_error_t.TOBII_ERROR_NO_ERROR);

        }


        void Update()
        {
            if (!_connection.IsServer)
            {
                applicationRect = new Rect(0, 0, Screen.width, Screen.height);
                // Get the mouse position
                Vector2 gaze = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                // Vector2 gaze = new Vector2(TobiiAPI.GetGazePoint().Screen.x, TobiiAPI.GetGazePoint().Screen.y);

                // transform.Find("Gaze").position = gaze;

                // Check if the mouse is within the application window
                if (applicationRect.Contains(gaze))
                {
                    // Debug.Log("Inside");
                    transform.Find("FramePanel").gameObject.SetActive(true);
                    FindObjectOfType<HumanInterface>().Visible = true;
                }
                else
                {
                    // Debug.Log("Outside");
                    transform.Find("FramePanel").gameObject.SetActive(false);
                    FindObjectOfType<HumanInterface>().Visible = false;
                }
            }
        }

    }
}
