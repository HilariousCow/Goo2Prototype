using System.Collections;
using System.Collections.Generic;
using Ludopathic.Goo.Managers;
using UnityEngine;

namespace Ludopathic.Goo.Managers
{
    public class CameraManager : MonoBehaviour
    {
        
        public BlobManager BlobManager;

        private Camera _camera;
        private Transform _transform;
        // Start is called before the first frame update
        void Start()
        {
            _transform = transform;
            _camera = GetComponent<Camera>();

        }

        // Update is called once per frame
        void LateUpdate()
        {


            Bounds bounds = BlobManager.OverallGooBounds;
          

            float screenRatio = Screen.width / (float)Screen.height;
            float largerSide = Mathf.Max(bounds.size.x, bounds.size.z * screenRatio);

            float heightAbove = Mathf.Atan(_camera.fieldOfView) * largerSide ;
            
            _transform.position = bounds.center + Vector3.up * heightAbove;
            _transform.rotation = Quaternion.LookRotation( bounds.center-transform.position, Vector3.forward);


            bounds.Encapsulate(Vector3.down);
            Vector3 near = bounds.ClosestPoint(_transform.position);
            Vector3 far = bounds.center - (near - bounds.center);


            float nearPos = Vector3.Dot((near - transform.position), _transform.forward);
            float farPos = Vector3.Dot((far - transform.position), _transform.forward);

            _camera.nearClipPlane =  Mathf.Min(nearPos, farPos)-1f;
            _camera.farClipPlane =  Mathf.Max(nearPos, farPos)+1f;

            
        
        }
        
        void OnDrawGizmosSelected()
        {
            // Draw a yellow cube at the transform position
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireCube(BlobManager.OverallGooBounds.center, BlobManager.OverallGooBounds.size);
        }
        
    }

}