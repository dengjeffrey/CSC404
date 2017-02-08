﻿using System.Collections;
using System.Linq;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private const int CastMask = 1 << Layers.Solid;
    private const float CastRadius = 0.1f;
    private const float MoveDurationInSeconds = 0.25f;

    private bool _isMoving;
    private float _moveTimer;


    void Start()
    {
        Physics.IgnoreLayerCollision(Layers.Player, Layers.Hole);
    }

    void Update()
    {
        if (!_isMoving)
        {
            transform.position = new Vector3(Mathf.RoundToInt(transform.position.x), transform.position.y,
                Mathf.RoundToInt(transform.position.z));
        }
    }

    public void Move(Vector3 direction)
    {
        if (_isMoving)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(direction, Vector3.up));


        var canPlayerMoveInDirection = IsOpen(transform.position + direction);
        var canPlayerJumpInDirection = IsOpen(transform.position + direction + Vector3.up);

        if (canPlayerMoveInDirection)
        {
            StartCoroutine(MoveCoroutine(new[] { transform }, direction));
        }
        else if (canPlayerJumpInDirection)
        {
            StartCoroutine(MoveCoroutine(new[] { transform }, direction + Vector3.up));
        }
    }

    public void TryPushBlock()
    {
        if (!IsOpen(transform.position + transform.forward) && IsOpen(transform.position + transform.forward * 2))
        {
            StartCoroutine(MoveCoroutine(new[] { GetBlockInFront().transform }, transform.forward));
        }
    }

    public void TryPullBlock()
    {
        if (!IsOpen(transform.position + transform.forward))
        {
            StartCoroutine(MoveCoroutine(new[] {transform}, Vector3.up));
            StartCoroutine(MoveCoroutine(new[] {GetBlockInFront().transform}, -transform.forward));
        }
    }

    private bool IsOpen(Vector3 position)
    {
        var colliders = Physics.OverlapSphere(position, CastRadius, CastMask);
        return colliders.Length == 0;
    }

    private Collider GetBlockInFront()
    {
        return Physics.OverlapSphere(transform.position + transform.forward, CastRadius, CastMask)[0];
    }

    private IEnumerator MoveCoroutine(Transform[] movedTransforms, Vector3 direction)
    {
        _isMoving = true;
        var oldPositions = movedTransforms.Select(i => i.position).ToList();
        _moveTimer = 0;

        while (_moveTimer < MoveDurationInSeconds)
        {
            yield return new WaitForEndOfFrame();
            _moveTimer += Time.deltaTime;
            for (var i = 0; i < movedTransforms.Length; i++)
            {
                movedTransforms[i].position = Vector3.Lerp(oldPositions[i], oldPositions[i] + direction,
                    Mathf.Pow(_moveTimer / MoveDurationInSeconds, 0.25f));
            }
        }

        for (var i = 0; i < movedTransforms.Length; i++)
        {
            movedTransforms[i].position = oldPositions[i] + direction;
        }

        _isMoving = false;
    }
}