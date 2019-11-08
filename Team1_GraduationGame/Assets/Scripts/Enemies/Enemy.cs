﻿namespace Team1_GraduationGame.Enemies
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Team1_GraduationGame.Events;
    using Team1_GraduationGame.Interaction;
    using UnityEngine.AI;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    [RequireComponent(typeof(SphereCollider), typeof(NavMeshAgent), typeof(Interactable))]
    public class Enemy : MonoBehaviour
    {
        #region Variables
        // References:
        public BaseEnemy thisEnemy;
        public VoidEvent playerDiedEvent;
        public Light viewConeLight;
        public IntVariable playerMoveState;
        private GameObject _player;
        private Movement _movement;
        private NavMeshAgent _navMeshAgent;
        [HideInInspector] public List<GameObject> wayPoints;
        [HideInInspector] public GameObject parentWayPoint;

        // Public variables:
        public float wayPointReachRange = 1.0f, hearingSensitivity = 2.0f, minimumAlwaysDetectRange = 1.3f;
        public bool drawGizmos = true, useWaitTime, rotateAtWaypoints, loopWaypointRoutine = true, alwaysAggro;
        public Color normalConeColor = Color.yellow, aggroConeColor = Color.red;
        [HideInInspector] public bool useGlobalWaitTime = true;
        [HideInInspector] public float waitTime = 0.0f;

        // Private variables:
        private bool _active, _timerRunning, _destinationSet, _isRotating, _isAggro, 
            _isHugging, _rotatingAtWp, _inTriggerZone, _accelerating, _hearingDisabled, _goingReversePath;
        private NavMeshPath _path;
        private Vector3 _lastSighting;
        private Vector3[] _wayPointRotations;
        private SphereCollider _thisCollider;
        private int _currentWayPoint = 0, _state = 0, _layerMask;
        private float[] _waitTimes;
        private float _targetSpeed, _hearingDistance;

        #endregion

        #region Awake

        void Awake()
        {
            _player = GameObject.FindGameObjectWithTag("Player");
            _layerMask = ~LayerMask.GetMask("Enemies");

            if (_player != null && thisEnemy != null)
            {
                if (playerMoveState == null)
                {
                    _hearingDisabled = true;
                    Debug.LogError("Enemy hearing and light FOV disabled: Player move state scriptable object not attached");
                }
                _active = true;
            }
            else
                Debug.LogError("Enemy disabled: Player not found or scriptable object not attached!");

            if (_active)
            {
                _thisCollider = gameObject.GetComponent<SphereCollider>();

                if (_player.GetComponent<Movement>() != null)
                    _movement = _player.GetComponent<Movement>();

                if (_thisCollider != null)
                {
                    _thisCollider.isTrigger = true;
                    _thisCollider.radius = thisEnemy.viewDistance;
                }

                if (GetComponent<NavMeshAgent>() != null)
                {
                    _navMeshAgent = GetComponent<NavMeshAgent>();
                    _navMeshAgent.speed = thisEnemy.walkSpeed;
                    _navMeshAgent.angularSpeed = thisEnemy.walkTurnSpeed;
                    _path = new NavMeshPath();
                }
                else
                {
                    _active = false;
                    Debug.LogError("Enemy Error: No Nav Mesh Agent script attached!");
                }

                if (wayPoints == null)
                    wayPoints = new List<GameObject>();
                

                if (rotateAtWaypoints)
                    _wayPointRotations = new Vector3[wayPoints.Count];

                _waitTimes = new float[wayPoints.Count];
                for (int i = 0; i < wayPoints.Count; i++)
                {
                    if (wayPoints[i].GetComponent<WayPoint>() != null)
                    {
                        _waitTimes[i] = wayPoints[i].GetComponent<WayPoint>().specificWaitTime;
                        if (rotateAtWaypoints)
                            _wayPointRotations[i] = new Vector3(0, wayPoints[i].GetComponent<WayPoint>().enemyLookDirection, 0);
                    }
                }

                if (viewConeLight != null)
                {
                    viewConeLight.range = thisEnemy.viewDistance;
                    viewConeLight.color = normalConeColor;
                    viewConeLight.spotAngle = thisEnemy.fieldOfView;
                    if (_hearingDisabled)
                        viewConeLight.gameObject.SetActive(false);
                }

                if (alwaysAggro)
                    _isAggro = true;
            }
        }
        #endregion

        /// <summary>
        /// 0 = Walking, 1 = Running, 2 = Attacking
        /// </summary>
        public void SwitchState(int state)
        {
            _state = state;

            if (_accelerating)
                _accelerating = false;
            
            switch (state)
            {
                case 0:
                    _targetSpeed = thisEnemy.walkSpeed;
                    _navMeshAgent.angularSpeed = thisEnemy.walkTurnSpeed;
                    break;
                case 1:
                    if (!thisEnemy.canRun)
                        return;
                    _targetSpeed = thisEnemy.runSpeed;
                    _navMeshAgent.angularSpeed = thisEnemy.runTurnSpeed;
                    break;
                case 2:
                    _targetSpeed = 0;
                    _navMeshAgent.speed = 0;
                    _active = false;
                    break;
                default:
                    Debug.LogError("Enemy Error: trying to switch to state that doesn't exist!'");
                    break;
            }

            _accelerating = true; // Enables accelerating in the update loop to desired state

        }

        private void FixedUpdate()
        {
            if (_active)
            {

                if (!_isAggro && wayPoints != null && wayPoints.Count != 0)
                {
                    if (_state != 0)
                        SwitchState(0); // Switch to walking

                    if (Vector3.Distance(transform.position, wayPoints[_currentWayPoint].transform.position) < wayPointReachRange)
                    {   
                        if (!rotateAtWaypoints)
                            _isRotating = false;
                        else
                            _rotatingAtWp = true;

                        UpdatePathRoutine();
                    }

                    if (!_destinationSet)   // If no destination set
                    {
                        _navMeshAgent.SetDestination(wayPoints[_currentWayPoint].transform.position);
                        _rotatingAtWp = false;
                        _destinationSet = true;
                        _isRotating = true;
                    }
                }

                if (alwaysAggro)
                {
                    _lastSighting = _player.transform.position;
                }

                if (_isAggro)
                {
                    if (!_isHugging && _state != 1)
                        SwitchState(1); // Switch to running

                    _navMeshAgent.SetDestination(_lastSighting);
                    _isRotating = true;

                    if (Vector3.Distance(transform.position, _player.transform.position) <
                        thisEnemy.embraceDistance && _isAggro)
                    {
                        if (!_isHugging)
                            StartCoroutine(EnemyHug());
                    }

                    if (Vector3.Distance(transform.position, _lastSighting) < thisEnemy.embraceDistance)
                    {
                        _destinationSet = false;
                        _isAggro = false;
                    }
                }

                if (_isRotating)
                {
                    Quaternion lookRotation;

                    if (!_rotatingAtWp || _isAggro)
                        lookRotation = _navMeshAgent.velocity.normalized != Vector3.zero ? Quaternion.LookRotation(_navMeshAgent.velocity.normalized) : transform.rotation;
                    else
                        lookRotation = Quaternion.Euler(_wayPointRotations[_currentWayPoint]) != Quaternion.identity ? Quaternion.Euler(_wayPointRotations[_currentWayPoint]) : transform.rotation;

                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, _navMeshAgent.angularSpeed);

                    #if UNITY_EDITOR
                    Vector3 fwd = lookRotation * Vector3.forward;
                    Debug.DrawRay(transform.position, fwd, Color.magenta, 0f, true);
                    #endif
                }

                if (_accelerating && _state != 2)
                {
                    if (_navMeshAgent.speed < _targetSpeed)
                    {
                        _navMeshAgent.speed += thisEnemy.AccelerationTime * Time.fixedDeltaTime;
                        if (_navMeshAgent.speed >= _targetSpeed)
                            _accelerating = false;
                    }
                    else if (_navMeshAgent.speed > _targetSpeed)
                    {
                        _navMeshAgent.speed -= thisEnemy.DeAccelerationTime * Time.fixedDeltaTime;
                        if (_navMeshAgent.speed <= _targetSpeed)
                            _accelerating = false;
                    }
                }

                if (!_hearingDisabled)
                {
                    _hearingDistance = thisEnemy.hearingDistance;
                    if (playerMoveState.value == 2)
                        _hearingDistance = thisEnemy.hearingDistance * hearingSensitivity;

                    if (HearingPathLength() < thisEnemy.hearingDistance && playerMoveState.value != 0 && playerMoveState.value != 1)
                        PlayerHeard();
                    else if (Vector3.Distance(transform.position, _player.transform.position) < minimumAlwaysDetectRange) // If very very close the enemy will hear the player no matter what
                        PlayerHeard();
                }

                ViewLightConeControl();

            }
        }

        private void UpdatePathRoutine()
        {
            if (!useWaitTime)
            {
                if (loopWaypointRoutine)
                    _currentWayPoint = (_currentWayPoint + 1) % wayPoints.Count;
                else
                {
                    if (!_goingReversePath)
                    {
                        if (_currentWayPoint + 1 > wayPoints.Count - 1)
                            _goingReversePath = true;
                        else
                            _currentWayPoint = (_currentWayPoint + 1);
                    }
                    else if (_goingReversePath)
                    {
                        if (_currentWayPoint - 1 < 0)
                            _goingReversePath = false;
                        else
                            _currentWayPoint = (_currentWayPoint - 1);
                    }
                }

                _destinationSet = false;
            }
            else if (useWaitTime && !_timerRunning)
            {
                StartCoroutine(WaitTimer());
                _timerRunning = true;
            }
        }

        private void OnTriggerStay(Collider col)
        {
            if (_active && !alwaysAggro)
                if (col.tag == _player.tag)
                {
                    Vector3 dir = _player.transform.position - transform.position;
                    float enemyToPlayerAngle = Vector3.Angle(transform.forward, dir);

                    if (enemyToPlayerAngle < thisEnemy.fieldOfView / 2)
                    {
                        RaycastHit hit;

                        if (Physics.Raycast(transform.position + transform.up, dir, out hit, thisEnemy.viewDistance, _layerMask))
                            if (hit.collider.tag == _player.tag)
                            {
                                _lastSighting = _player.transform.position;
                                if (!_isAggro)
                                    StartCoroutine(EnemyAggro());
                            }
                    }
                    else if (_isAggro)
                    {
                        _lastSighting = _player.transform.position;
                    }

                    _inTriggerZone = true;
                }
        }

        private void OnTriggerExit(Collider col)
        {
            if (_active)
                if (col.tag == _player.tag)
                {
                    _inTriggerZone = false;
                }
        }

        private void PlayerHeard()
        {
            _lastSighting = _player.transform.position;

            if (!_isAggro)
                StartCoroutine(EnemyAggro());
        }

        public void PushDown()
        {
            if (thisEnemy != null && _active)
            {
                _active = false;
                _navMeshAgent.isStopped = true;

                // TODO - YYY play lie down/knocked down animation

                viewConeLight.gameObject.SetActive(true);
                viewConeLight.color = Color.green;

                StopCoroutine(EnemyHug());  // Stop hug if hugging

                if (_movement != null)
                {
                    _movement.Frozen(false);
                }

                StartCoroutine(PushDownDelay());
            }
        }

        private void ViewLightConeControl()
        {
            if (viewConeLight != null && playerMoveState != null)
            {
                if (_isAggro && viewConeLight.color != aggroConeColor)
                    viewConeLight.color = aggroConeColor;
                else if (!_isAggro && viewConeLight.color != normalConeColor)
                    viewConeLight.color = normalConeColor;

                if (playerMoveState.value == 0 || playerMoveState.value == 1)
                {
                    if (!viewConeLight.gameObject.activeSelf)
                        viewConeLight.gameObject.SetActive(true);
                }
                else if (viewConeLight.gameObject.activeSelf)
                    viewConeLight.gameObject.SetActive(false);
            }
        }

        private float HearingPathLength()
        {
            _navMeshAgent.CalculatePath(_player.transform.position, _path);

            Vector3[] allPathPoints = new Vector3[_path.corners.Length + 2];
            allPathPoints[0] = transform.position;
            allPathPoints[allPathPoints.Length - 1] = _player.transform.position;

            for (int i = 0; i < _path.corners.Length; i++)
            {
                allPathPoints[i + 1] = _path.corners[i];
            }

            float tempPathLength = 0.0f;

            for (int i = 0; i < allPathPoints.Length - 1; i++)
            {
                tempPathLength += Vector3.Distance(allPathPoints[i], allPathPoints[i + 1]);
            }

            return tempPathLength;
        }

        #region Co-Routines

        private IEnumerator WaitTimer()
        {
            if (_waitTimes[_currentWayPoint] > 0)
                yield return new WaitForSeconds(_waitTimes[_currentWayPoint]);
            else if (useGlobalWaitTime)
                yield return new WaitForSeconds(waitTime);

            _currentWayPoint = (_currentWayPoint + 1) % wayPoints.Count;
            _destinationSet = false;
            _timerRunning = false;
        }

        private IEnumerator EnemyAggro()
        {
            _isAggro = true;
            yield return new WaitForSeconds(thisEnemy.aggroTime);
            Debug.Log("Is AGGRO");
            if (!_inTriggerZone)
                _isAggro = false;

            if (!_isAggro)
                _destinationSet = false;
        }

        private IEnumerator EnemyHug()
        {
            _isHugging = true;
            SwitchState(2); // Switch to attacking

            transform.LookAt(_player.transform.position);

            if (_movement != null)
            {
                _movement.Frozen(true);
            }

            yield return new WaitForSeconds(thisEnemy.embraceDelay);

            if (Vector3.Distance(transform.position, _player.transform.position) <
                thisEnemy.embraceDistance)
            {
                Debug.Log("THE PLAYER DIED");

                if (playerDiedEvent != null)
                {
                    playerDiedEvent.Raise();
                }
            }
            else
            {
                _active = true;
                _isHugging = false;
                if (!alwaysAggro)
                    _isAggro = false;
                else
                    _isAggro = true;
            }
        }

        private IEnumerator PushDownDelay()
        {
            yield return new WaitForSeconds(thisEnemy.pushedDownDuration);
            _active = true;
            _navMeshAgent.isStopped = false;
            viewConeLight.color = normalConeColor;

            if (!alwaysAggro)
                _destinationSet = false;
            else
                _isAggro = true;
        }

        #endregion

        #region Setter and Getters
        public void SetIsActive(bool isActive) { _active = isActive; }
        public bool GetIsActive() { return _active; }
        public bool GetHearing() { return _hearingDisabled; }
        public NavMeshAgent getNavMeshAgent() { return _navMeshAgent; }
        public void SetAggro(bool _aggro) { _isAggro = _aggro; }
        public void SetLastSighting(Vector3 location) { _lastSighting = location; }
        #endregion

#if UNITY_EDITOR
        #region WayPoint System
        public void AddWayPoint()
        {
            if (Application.isEditor)
            {
                GameObject tempWayPointObj;

                if (!GameObject.Find("EnemyWaypoints"))
                    new GameObject("EnemyWaypoints");

                if (!GameObject.Find(gameObject.name + "_Waypoints"))
                {
                    parentWayPoint = new GameObject(gameObject.name + "_Waypoints");

                    parentWayPoint.AddComponent<WayPoint>();
                    parentWayPoint.GetComponent<WayPoint>().isParent = true;
                    parentWayPoint.transform.parent =
                        GameObject.Find("EnemyWaypoints").transform;
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
                tempWayPointObj.AddComponent<WayPoint>();
                WayPoint tempWayPointScript = tempWayPointObj.GetComponent<WayPoint>();
                tempWayPointScript.wayPointId = wayPoints.Count + 1;
                tempWayPointScript.parentEnemy = gameObject;
                tempWayPointScript.parentWayPoint = parentWayPoint;

                tempWayPointObj.transform.position = gameObject.transform.position;
                tempWayPointObj.transform.parent = parentWayPoint.transform;
                wayPoints.Add(tempWayPointObj);
            }
        }

        public void RemoveWayPoint()
        {
            if (Application.isEditor)
            {
                if (wayPoints != null)
                    if (wayPoints.Count > 0)
                    {
                        DestroyImmediate(wayPoints[wayPoints.Count - 1].gameObject);
                    }
            }
        }

        private void OnDrawGizmos()
        {
            if (drawGizmos && Application.isEditor)
                if (wayPoints != null)
                {
                    for (int i = 0; i < wayPoints.Count; i++)
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawWireSphere(wayPoints[i].transform.position, 0.2f);
                        Handles.Label(wayPoints[i].transform.position + (Vector3.up * 0.6f), (i + 1).ToString());

                        Gizmos.DrawLine(transform.position, wayPoints[i].transform.position);

                        Gizmos.color = Color.yellow;
                        if (i + 1 < wayPoints.Count)
                        {
                            Gizmos.DrawLine(wayPoints[i].transform.position, wayPoints[i + 1].transform.position);

                        }
                        else if (i == wayPoints.Count - 1 && wayPoints.Count > 1 && loopWaypointRoutine)
                        {
                            Gizmos.DrawLine(wayPoints[wayPoints.Count - 1].transform.position, wayPoints[0].transform.position);
                        }

                        if (thisEnemy != null)
                        {
                            Gizmos.color = Color.red;
                            Gizmos.DrawLine(transform.position + transform.up, transform.forward * thisEnemy.viewDistance + (transform.position + transform.up));

                        }
                    }
                }
        }
        #endregion
#endif
    }
}