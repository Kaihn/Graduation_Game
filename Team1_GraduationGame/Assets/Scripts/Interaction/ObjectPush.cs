﻿using System.Collections.Generic;
using Team1_GraduationGame.Enemies;
using UnityEngine;
using UnityEngine.ProBuilder.MeshOperations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Team1_GraduationGame.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    public class ObjectPush : MonoBehaviour
    {
        // References:
        private Rigidbody _thisRigidBody;
        private Interactable _interactable;
        private GameObject _player;

        // Public:
        public bool drawGizmos = true;
        [Range(0.1f, 5.0f)] public float thrustAmount = 1.0f;
        [HideInInspector] public List<GameObject> wayPoints;
        [HideInInspector] public GameObject parentWayPoint;

        // Private:

        private void Awake()
        {
            _player = GameObject.FindGameObjectWithTag("Player");
            _thisRigidBody = GetComponent<Rigidbody>();
            _interactable = GetComponent<Interactable>();
            
            if (wayPoints == null)
                wayPoints = new List<GameObject>();

            _thisRigidBody.constraints = RigidbodyConstraints.FreezeAll;    // TODO find better solution
        }

        /// <summary>
        /// Pushes an object using add force.
        /// </summary>
        /// <param name="ignoreWaypoints">If TRUE the object will be pushed in direction of forward vector instead of waypoint.</param>
        public void Push(bool ignoreWaypoints)
        {
            if (_player != null && _thisRigidBody != null)
            {
                _thisRigidBody.constraints = RigidbodyConstraints.None;

                if (ignoreWaypoints)
                    _thisRigidBody.AddForce(transform.forward * thrustAmount, ForceMode.Impulse);
                else if (wayPoints != null && wayPoints.Count > 1)
                {
                    // if (_player) // Checks which side the player is standing to the object
                    _thisRigidBody.AddForce((wayPoints[wayPoints.Count - 1].transform.position - transform.position) * thrustAmount, ForceMode.Impulse);
                    Debug.Log("YEET " + gameObject.name);
                    // else if (_player)
                    // _thisRigidBody.AddForce((wayPoints[wayPoints.Count - 1].transform.position - transform.position) * thrustAmount, ForceMode.Impulse);
                }
            }
        }

        //private void OnCollisionEnter(Collision col)
        //{
        //    if (col.collider.tag == "Player" || col.collider.tag == "Enemy")
        //        _thisRigidBody.constraints = RigidbodyConstraints.FreezeAll;
        //}


        #region Waypoint System
        public void AddWayPoint()
        {
            GameObject tempWayPointObj;

            if (!GameObject.Find("ObjectMovingWaypoints"))
                new GameObject("ObjectMovingWaypoints");

            if (!GameObject.Find(gameObject.name + "_Waypoints"))
            {
                parentWayPoint = new GameObject(gameObject.name + "_Waypoints");

                parentWayPoint.AddComponent<ObjectWayPoint>();
                parentWayPoint.GetComponent<ObjectWayPoint>().isParent = true;
                parentWayPoint.transform.parent =
                    GameObject.Find("ObjectMovingWaypoints").transform;
            }
            else
            {
                parentWayPoint = GameObject.Find(gameObject.name + "_Waypoints");
            }

            if (wayPoints == null)
            {
                wayPoints = new List<GameObject>();
            }

            tempWayPointObj = new GameObject("WayPoint" + (wayPoints.Count + 1));
            tempWayPointObj.AddComponent<ObjectWayPoint>();
            ObjectWayPoint tempWayPointScript = tempWayPointObj.GetComponent<ObjectWayPoint>();
            tempWayPointScript.wayPointId = wayPoints.Count + 1;
            tempWayPointScript.parentObject = gameObject;
            tempWayPointScript.parentWayPoint = parentWayPoint;

            tempWayPointObj.transform.position = gameObject.transform.position;
            tempWayPointObj.transform.parent = parentWayPoint.transform;
            wayPoints.Add(tempWayPointObj);
        }

        public void RemoveWayPoint()
        {
            if (wayPoints != null)
                if (wayPoints.Count > 0)
                {
                    DestroyImmediate(wayPoints[wayPoints.Count - 1].gameObject);
                }
        }
        #endregion

        #region Draw Gizmos
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (drawGizmos)
                if (wayPoints != null)
                {
                    for (int i = 0; i < wayPoints.Count; i++)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireSphere(wayPoints[i].transform.position, 0.1f);
                        Handles.Label(wayPoints[i].transform.position + (Vector3.up * 0.5f), (i + 1).ToString());

                        Gizmos.color = Color.white;
                        if (i + 1 < wayPoints.Count)
                        {
                            Gizmos.DrawLine(wayPoints[i].transform.position, wayPoints[i + 1].transform.position);

                        }
                    }
                }
        }
#endif
        #endregion
    }

    #region Custom Inspector
#if UNITY_EDITOR
    [CustomEditor(typeof(ObjectPush))]
    public class ObjectPush_Inspector : UnityEditor.Editor
    {
        private GUIStyle _style = new GUIStyle();
        private GameObject _parentWayPoint;
        private bool _runOnce;

        public override void OnInspectorGUI()
        {
            if (!_runOnce)
            {
                _style.fontStyle = FontStyle.Bold;
                _style.alignment = TextAnchor.MiddleCenter;
                _style.fontSize = 14;
                _runOnce = true;
            }

            EditorGUILayout.HelpBox("Please only use the 'Add WayPoint' button to create new waypoints. They can then be found in the 'WayPoints' object in the hierarchy.", MessageType.Info);

            DrawDefaultInspector(); // for other non-HideInInspector fields

            var script = target as ObjectPush;

            DrawUILine(false);
            if (script.wayPoints != null)
            {
                if (script.wayPoints.Count == 0)
                {
                    _style.normal.textColor = Color.red;
                }
                else
                {
                    _style.normal.textColor = Color.green;
                }

                EditorGUILayout.LabelField(script.wayPoints.Count.ToString() + " waypoints", _style);
            }
            else
            {
                _style.normal.textColor = Color.red;
                EditorGUILayout.LabelField("0 waypoints", _style);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Add WayPoint"))
            {
                script.AddWayPoint();
            }

            if (GUILayout.Button("Remove WayPoint"))
            {
                script.RemoveWayPoint();
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(script);
            }
        }

        #region DrawUILine function
        public static void DrawUILine(bool start)
        {
            Color color = new Color(1, 1, 1, 0.3f);
            int thickness = 1;
            if (start)
                thickness = 4;
            int padding = 8;

            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            r.height = thickness;
            r.y += padding / 2;
            r.x -= 2;
            r.width += 6;
            EditorGUI.DrawRect(r, color);
        }
        #endregion
    }
#endif
    #endregion
}

