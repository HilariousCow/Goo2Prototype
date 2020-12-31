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

            _transform.position = BlobManager.OverallGooBounds.center + (Vector3.up * (BlobManager.OverallGooBounds.size.magnitude * 2.6f));
            _transform.rotation = Quaternion.LookRotation( BlobManager.OverallGooBounds.center-transform.position, Vector3.forward);


            Vector3 near = BlobManager.OverallGooBounds.ClosestPoint(_transform.position);
            Vector3 far = BlobManager.OverallGooBounds.center - (near - BlobManager.OverallGooBounds.center);


            float nearPos = Vector3.Dot((near - transform.position), _transform.forward);
            float farPos = Vector3.Dot((far - transform.position), _transform.forward);

            _camera.nearClipPlane = Mathf.Min(nearPos, farPos)-1f;
            _camera.farClipPlane =  Mathf.Max(nearPos, farPos)+1f;

            
        
        }
        
        void OnDrawGizmosSelected()
        {
            // Draw a yellow cube at the transform position
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(BlobManager.OverallGooBounds.center, BlobManager.OverallGooBounds.size);
        }
        
    }

}