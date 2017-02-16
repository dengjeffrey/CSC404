﻿using System.Collections;
using System.Linq;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Multiplier used to add a Vector3.down force, to increase falling speed of the player
    public float GravityMultiplier = 80.0f;
    public GameObject Wall;

    private const int CastMask = 1 << Layers.Solid;
    private const float CastRadius = 0.1f;
    private const float MoveDurationInSeconds = 0.25f;

    // The time in seconds that a column stays locked after a block was pulled
    // or pushed from it
    private const float ColumnLockDurationInSeconds = MoveDurationInSeconds + 0.5f;

    // The number of blocks in a column to lock from the bottom of the stack
    private const float ColumnLockDistance = 20;

    private bool _isMoving;
    private bool _isFalling;
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

    void FixedUpdate () {
        // Add faster falling speed to match jumping speed
        if (!_isMoving)
        {
            GetComponent<Rigidbody>().AddForce(Vector3.down * GravityMultiplier * GetComponent<Rigidbody>().mass);
        }
    }

    void OnTriggerEnter(Collider other) {
        GameObject collidingObject = other.gameObject;
        if (collidingObject.tag == "Block") {
            if (gameObject.transform.position.x - collidingObject.transform.position.x < 0.5
                && gameObject.transform.position.y < collidingObject.transform.position.y) {
                Destroy (gameObject);
            }
        }
    }

    void OnCollisionEnter(Collision collision) {
        if (collision.transform.position.y < transform.position.y)
        {
            _isFalling = false;
        }
    }

    public void Move(Vector3 direction)
    {
        if (_isMoving || _isFalling)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(direction, Vector3.up));

        var canPlayerMoveInDirection = IsOpen(transform.position + direction);
        var canPlayerJumpInDirection = IsOpen(transform.position + direction + Vector3.up);

        if (canPlayerMoveInDirection)
        {
            // Check if player is jumping down, check for the platform under direction
            var isPlayerJumpingDownInDirection = IsOpen(transform.position + direction + Vector3.down);

            if (isPlayerJumpingDownInDirection) {
                // Lock movement till player has reached the bottom
                _isFalling = true;
            }

            StartCoroutine(MoveCoroutine(new[] { transform }, direction));
        }
        else if (canPlayerJumpInDirection)
        {
            StartCoroutine(MoveCoroutine(new[] { transform }, direction + Vector3.up));
        }
    }

    public void TryPushBlock()
    {
        if (!IsOpen(transform.position + transform.forward) &&
            IsOpen(transform.position + transform.forward * 2) &&
            !_isFalling)
        {
            var block = GetBlockInFront();
            var direction = transform.forward;

            var moveable = block.gameObject.GetComponent<PlayerMoveable>();
            if (moveable.isLocked)
                return;

            MoveBlockInDirection(block, direction);
        }
    }

    public void TryPullBlock()
    {
        if (!IsOpen(transform.position + transform.forward) && !_isFalling)
        {
            var block = GetBlockInFront();
            var direction = -transform.forward;

            var moveable = block.gameObject.GetComponent<PlayerMoveable>();
            if (moveable.isLocked)
                return;

            MoveBlockInDirection(block, direction);
            StartCoroutine(MoveCoroutine(new[] {transform}, Vector3.up));

        }
    }

    private void MoveBlockInDirection(Collider block, Vector3 direction)
    {
        var moveable = block.gameObject.GetComponent<PlayerMoveable>();
        moveable.finalDestination = block.transform.position + direction;
        LockColumnFromPosition(block.transform.position);

        // Let the Wall know that a part of it needs to regenerate
        // If the block is still in the initial middle row
        if (block.transform.parent.gameObject.tag == Tags.Wall &&
            block.transform.position.z == 0) {
            RegenerateWallForBlock(block);
        }

        StartCoroutine(MoveCoroutine(new[] { block.transform }, direction));
    }

    /*
     * Notifies the Wall that a block needs to be added for the block being removed
     */
    private void RegenerateWallForBlock(Collider block)
    {
        var generator = Wall.GetComponent<BlockGenerator>();
        var blockPosition = block.transform.localPosition;
        generator.AddBlockAtTop(
            new Vector2(Mathf.RoundToInt(blockPosition.x),
                        Mathf.RoundToInt(blockPosition.z)));
    }

    private bool IsOpen(Vector3 position)
    {
        var colliders = Physics.OverlapSphere(position, CastRadius, CastMask);
        return colliders.Length == 0;
    }

    private Collider GetBlockInFront()
    {
        return
            Physics.OverlapSphere(transform.position + transform.forward,
                                  CastRadius,
                                  CastMask,
                                  QueryTriggerInteraction.Ignore)[0];
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

    /*
     * Given Vector3 position;
     * Lock blocks located from (position.x, bottom of the game, position.z) to (position.x, ColumnLockDistance, position.z)
     * excluding the block at position.
     */
    private void LockColumnFromPosition(Vector3 position)
    {
        var bottomObjectTag = Tags.GarbageCollider;
        var bottomObjects =  GameObject.FindGameObjectsWithTag(bottomObjectTag);

        if (bottomObjects.Length == 0)
        {
            Debug.Log("Error at PlayerController.LockColumnFromPosition(): No object was found with tag " + bottomObjectTag);
            return;
        }

        var positionAtBottom = new Vector3(position.x, bottomObjects[0].transform.position.y, position.z);
        var hits =
            Physics.RaycastAll(positionAtBottom,
                               Vector3.up,
                               ColumnLockDistance,
                               CastMask,
                               QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            var gameObject = hit.rigidbody.gameObject;

            if (gameObject.tag == "Block" && gameObject.transform.position != position)
            {
                gameObject.GetComponent<PlayerMoveable>().SetLockedForDuration(ColumnLockDurationInSeconds);
            }
        }
    }
}
