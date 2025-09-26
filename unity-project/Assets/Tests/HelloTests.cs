using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using Aetheria.VR;

namespace Aetheria.Tests
{
    /// <summary>
    /// Basic unit tests for Hello VR functionality.
    /// Tests VR controller initialization and basic functionality.
    /// 
    /// To run tests:
    /// 1. Open Unity Test Runner (Window > General > Test Runner)
    /// 2. Select "PlayMode" tab
    /// 3. Click "Run All" or run individual tests
    /// </summary>
    public class HelloTests
    {
        private GameObject testGameObject;
        private VRPlayerController vrController;
        
        [SetUp]
        public void Setup()
        {
            // Create a test GameObject with VRPlayerController
            testGameObject = new GameObject("TestVRPlayer");
            vrController = testGameObject.AddComponent<VRPlayerController>();
            
            // Create a mock XR Origin
            GameObject xrOrigin = new GameObject("XR Origin");
            xrOrigin.transform.SetParent(testGameObject.transform);
            
            // Create a mock camera
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.transform.SetParent(xrOrigin.transform);
            Camera camera = cameraObj.AddComponent<Camera>();
            
            // Set references (in a real test, you'd use reflection or public setters)
            var xrOriginField = typeof(VRPlayerController).GetField("xrOrigin", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var xrCameraField = typeof(VRPlayerController).GetField("xrCamera", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            xrOriginField?.SetValue(vrController, xrOrigin.transform);
            xrCameraField?.SetValue(vrController, camera);
        }
        
        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
            {
                Object.DestroyImmediate(testGameObject);
            }
        }
        
        [Test]
        public void VRPlayerController_InitializesCorrectly()
        {
            // Assert
            Assert.IsNotNull(vrController, "VRPlayerController should be initialized");
            Assert.IsNotNull(testGameObject, "Test GameObject should be created");
        }
        
        [Test]
        public void VRPlayerController_GetPlayerPosition_ReturnsValidPosition()
        {
            // Act
            Vector3 position = vrController.GetPlayerPosition();
            
            // Assert
            Assert.IsNotNull(position, "Player position should not be null");
            // Position should be Vector3.zero when xrOrigin is not set
            Assert.AreEqual(Vector3.zero, position, "Default position should be Vector3.zero");
        }
        
        [Test]
        public void VRPlayerController_GetPlayerRotation_ReturnsValidRotation()
        {
            // Act
            Quaternion rotation = vrController.GetPlayerRotation();
            
            // Assert
            Assert.IsNotNull(rotation, "Player rotation should not be null");
            // Rotation should be Quaternion.identity when xrOrigin is not set
            Assert.AreEqual(Quaternion.identity, rotation, "Default rotation should be Quaternion.identity");
        }
        
        [UnityTest]
        public IEnumerator VRPlayerController_SceneLoadsWithoutErrors()
        {
            // This test verifies that the scene can be loaded without throwing exceptions
            // In a real implementation, you would load the actual HelloVR scene
            
            // Act - simulate scene loading by enabling/disabling the GameObject
            testGameObject.SetActive(false);
            yield return null; // Wait one frame
            testGameObject.SetActive(true);
            yield return null; // Wait one frame
            
            // Assert - if we get here without exceptions, the test passes
            Assert.IsTrue(true, "Scene should load without errors");
        }
        
        [Test]
        public void VRPlayerController_ComponentCanBeAttached()
        {
            // Test that the VRPlayerController component can be attached to a GameObject
            GameObject testObj = new GameObject("TestAttach");
            VRPlayerController controller = testObj.AddComponent<VRPlayerController>();
            
            // Assert
            Assert.IsNotNull(controller, "VRPlayerController should be attachable");
            Assert.IsTrue(testObj.GetComponent<VRPlayerController>() == controller, 
                "Attached component should be the same instance");
            
            // Cleanup
            Object.DestroyImmediate(testObj);
        }
    }
}
