﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/*
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

public class Warship : Agent, IDamagableObject
{
    public enum MoveCommand
    {
        IDLE = 0,
        FORWARD = 1,
        BACKWARD = 2,
        LEFT = 3,
        RIGHT = 4
    }

    public enum AttackCommand
    {
        IDLE = 0,
        FIRE = 1,
        //PITCH_UP = 2,
        //PITCH_DOWN = 3
    }

    public enum Direction
    {
        FORWARD = 0,
        FORWARD_RIGHT = 1,
        RIGHT = 2,
        BACKWARD_RIGHT = 3,
        BACKWARD = 4,
        BACKWARD_LEFT = 5,
        LEFT = 6,
        FORWARD_LEFT = 7
    }

    public const float k_MaxHealth = 10f;
    public Transform StartingPoint;
    public Color RendererColor;
    public ParticleSystem explosion;
    public Warship Target {
        get => m_Target;
        private set { m_Target = value; }
    }
    [HideInInspector] public Rigidbody rb;
    public int PlayerId;
    public int TeamId;
    [HideInInspector] public WeaponSystemsOfficer weaponSystemsOfficer;
    [HideInInspector] public float CurrentHealth {
        get => _currentHealth;
        private set { _currentHealth = value; }
    }
    [HideInInspector] public float AccumulatedDamage {
        get {
            float damage = _accumulatedDamage;
            _accumulatedDamage = 0;
            return damage;
        }
        private set { _accumulatedDamage = value; }
    }
    public Transform BattleField;
    public bool IsDestroyed { get => CurrentHealth <= 0f + Mathf.Epsilon; }
    public bool IsDetected = false;
    [HideInInspector] public Engine Engine { get; private set; }

    private TaskForce m_TaskForce;
    private Warship m_Target;
    private float[] m_RaycastHitDistances;
    private const int k_RaycastHitDirections = 8;

    private float _currentHealth = k_MaxHealth;
    private float _accumulatedDamage = 0;
    private bool m_IsCollisionWithWarship = false;

    private Vector3 m_AimingPoint;
    private const float k_AimingPointVerticalMin = -2f;
    private const float k_AimingPointVerticalMax = 1f;
    private WarshipModel[] m_AllyWarshipModels;
    private WarshipModel[] m_EnemyWarshipModels;

    public void Reset()
    {
        GetComponent<Rigidbody>().velocity = Vector3.zero;
        GetComponent<Rigidbody>().angularVelocity = Vector3.zero;

        transform.position = StartingPoint.position;
        transform.rotation = StartingPoint.rotation;

        CurrentHealth = k_MaxHealth;
        AccumulatedDamage = 0;
        m_IsCollisionWithWarship = false;

        m_AimingPoint = Vector3.zero;   // new Vector3(0f, transform.rotation.eulerAngles.y, 0f);
        weaponSystemsOfficer.Reset();
        Engine.Reset();

        for (int i = 0; i < k_RaycastHitDirections; i++)
        {
            m_RaycastHitDistances[i] = 1.0f;
        }

        IsDetected = false;

        m_AllyWarshipModels = new WarshipModel[m_TaskForce.Units.Length];
        for (int i = 0; i < m_AllyWarshipModels.Length; i++)
        {
            m_AllyWarshipModels[i] = m_TaskForce.Units[i].GenerateWarshipModel();
        }
        m_EnemyWarshipModels = new WarshipModel[m_TaskForce.TargetTaskForce.Units.Length];
        for (int i = 0; i < m_EnemyWarshipModels.Length; i++)
        {
            m_EnemyWarshipModels[i] = m_TaskForce.TargetTaskForce.Units[i].GenerateWarshipModel();
        }

        UpdateTarget();
    }

    // Start is called before the first frame update
    void Start()
    {
        Init();
    }

    // Update is called once per frame
    void Update()
    {
        // Drawn
        if (IsDestroyed)
        {
            Vector3 position = transform.position;
            Vector3 underwaterPosition = new Vector3(position.x, -20f, position.z);
            transform.position = Vector3.Lerp(position, underwaterPosition, Time.deltaTime);
        }
    }

    void FixedUpdate()
    {
        Warship target = m_Target;
        if (target == null)
        {
            return;
        }

        Vector3 rotation = Vector3.zero;
        rotation.y = Geometry.GetAngleBetween(transform.position, target.transform.position);

        // Auto-Targeting
        // Vector3 projectilexz = transform.position;
        // projectilexz.y = 0f;
        // Vector3 targetxz = target.transform.position;
        // targetxz.y = 0f;
        // float r = Vector3.Distance(projectilexz, targetxz);
        // float G = Physics.gravity.y;
        // float vz = 8000f;
        // rotation.x = Mathf.Atan((G * r) / (vz * 2f)) * Mathf.Rad2Deg;   // max: 140
        rotation.x = m_AimingPoint.x;

        weaponSystemsOfficer.Aim(Quaternion.Euler(rotation));
    }

    private void Init()
    {
        m_TaskForce = GetComponentInParent<TaskForce>();
        // Debug.Log($"[Warship({PlayerId})] TaskForce: {m_TaskForce}");

        weaponSystemsOfficer = GetComponent<WeaponSystemsOfficer>();
        weaponSystemsOfficer.Assign(TeamId, PlayerId);

        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        Engine = GetComponent<Engine>();

        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            meshRenderers[i].material.color = RendererColor;
        }

        m_RaycastHitDistances = new float[k_RaycastHitDirections];

        Reset();
    }

    public void UpdateTarget()
    {
        Warship newTarget = null;
        Vector3 position = transform.position;
        float distanceToTarget = Mathf.Infinity;
        foreach (var warship in m_TaskForce.TargetTaskForce.Units.Where(unit => !unit.IsDestroyed))
        {
            float distance = Vector3.Distance(position, warship.transform.position);
            if (distance < distanceToTarget)
            {
                newTarget = warship;
                distanceToTarget = distance;
            }
        }

        Target = newTarget;
    }

// #if !UNITY_EDITOR
    #region MLAgent
    public override void Initialize()
    {
        Init();
    }

    public override void OnEpisodeBegin()
    {
        Reset();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        UpdateTarget();

        for (int i = 0; i < m_AllyWarshipModels.Length; i++)
        {
            AddWarshipObservation(sensor, m_AllyWarshipModels[i]);
        }

        // FIXME
        int targetIndex = 0;
        Warship[] targetWarships = m_TaskForce.TargetTaskForce.Units;
        for (int i = 0; i < targetWarships.Length; i++)
        {
            Warship warship = targetWarships[i];
            AddWarshipObservation(sensor, m_EnemyWarshipModels[i]);

            if (warship == m_Target)
            {
                targetIndex = i;
            }
        }

        sensor.AddOneHotObservation(targetIndex, targetWarships.Length);    // Target Field (3) / 51

        // ======

        // Surrounding Detections (8)
        for (int i = 0; i < k_RaycastHitDirections; i++)
        {
            sensor.AddObservation(m_RaycastHitDistances[i] < 1.0f);
        }

        // Weapon (42)
        WeaponSystemsOfficer.BatterySummary[] batterySummary = weaponSystemsOfficer.Summary();
        for (int i = 0; i < batterySummary.Length; i++)
        {
            WeaponSystemsOfficer.BatterySummary battery = batterySummary[i];
            sensor.AddObservation(battery.rotation.x / Turret.k_VerticalMin);
            sensor.AddObservation(Mathf.Cos(battery.rotation.y));
            sensor.AddObservation(Mathf.Sin(battery.rotation.y));
            sensor.AddObservation(battery.isReloaded);
            sensor.AddObservation(battery.cooldown);
            sensor.AddObservation(battery.isDamaged);
            sensor.AddObservation(battery.repairProgress);
        }

        // Aiminig (6)
        Vector2 aimingPoint = new Vector2(m_AimingPoint.x, m_AimingPoint.y);
        sensor.AddOneHotObservation((int) (aimingPoint.x - k_AimingPointVerticalMin), (int) (k_AimingPointVerticalMax - k_AimingPointVerticalMin) + 1);
        sensor.AddObservation(Mathf.Cos(aimingPoint.y * Mathf.Deg2Rad));
        sensor.AddObservation(Mathf.Sin(aimingPoint.y * Mathf.Deg2Rad));

        sensor.AddObservation(weaponSystemsOfficer.Ammo / (float) WeaponSystemsOfficer.maxAmmo);

        sensor.AddOneHotObservation(Engine.SpeedLevel + 2, 5);
        sensor.AddOneHotObservation(Engine.SteerLevel + 2, 5);
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        if (IsDestroyed)
        {
            Engine.SetSpeedLevel(0);
            Engine.SetSteerLevel(0);
            return;
        }

        int movementAction = (int) vectorAction[0];
        int attackAction = (int) vectorAction[1];

        // Movement Actions
        switch (movementAction)
        {
            case (int) MoveCommand.IDLE:
                break;
            case (int) MoveCommand.FORWARD:
                Engine.SetSpeedLevel(Engine.SpeedLevel + 1);
                break;
            case (int) MoveCommand.BACKWARD:
                Engine.SetSpeedLevel(Engine.SpeedLevel - 1);
                break;
            case (int) MoveCommand.LEFT:
                Engine.SetSteerLevel(Engine.SteerLevel - 1);
                break;
            case (int) MoveCommand.RIGHT:
                Engine.SetSteerLevel(Engine.SteerLevel + 1);
                break;
        }

        // Attack Actions
        switch (attackAction)
        {
            case (int) AttackCommand.IDLE:
                break;
            case (int) AttackCommand.FIRE:
                uint usedAmmos = weaponSystemsOfficer.FireMainBattery();
                AddReward(-usedAmmos / 10000f);
                break;
        }

        // Default Time Penalty
        // FIXME:
        float[] allyHitpoints_t = new float[m_TaskForce.Units.Length];
        for (int i = 0; i < m_TaskForce.Units.Length; i++)
        {
            allyHitpoints_t[i] = m_AllyWarshipModels[i].CurrentHealth;
            m_AllyWarshipModels[i] = m_TaskForce.Units[i].GenerateWarshipModel();
        }

        float[] enemyHitpoint_t = new float[m_TaskForce.TargetTaskForce.Units.Length];
        for (int i = 0; i < m_TaskForce.TargetTaskForce.Units.Length; i++)
        {
            enemyHitpoint_t[i] = m_EnemyWarshipModels[i].CurrentHealth;

            Warship warship = m_TaskForce.TargetTaskForce.Units[i];
            if (warship.IsDetected)
            {
                m_EnemyWarshipModels[i] = warship.GenerateWarshipModel();
            }
        }

        float attackReward = 0f;
        for (int i = 0; i < m_TaskForce.TargetTaskForce.Units.Length; i++)
        {
            attackReward -= allyHitpoints_t[i] - m_AllyWarshipModels[i].CurrentHealth;
            attackReward += enemyHitpoint_t[i] - m_EnemyWarshipModels[i].CurrentHealth;

            if (allyHitpoints_t[i] >= 0f + Mathf.Epsilon && m_AllyWarshipModels[i].CurrentHealth <= 0f + Mathf.Epsilon)
            {
                attackReward -= 5f;
            }
            if (enemyHitpoint_t[i] >= 0f + Mathf.Epsilon && m_EnemyWarshipModels[i].CurrentHealth <= 0f + Mathf.Epsilon)
            {
                attackReward += 5f;
            }
        }
        AddReward(attackReward / 10f);

        CurrentHealth -= AccumulatedDamage;
        // FIXME: GroupReward
        // float hitpointReward = (CurrentHealth - m_PreviousHealth) - (target.CurrentHealth - m_PreviousOpponentHealth);
        // AddReward(hitpointReward / k_MaxHealth);
        // m_PreviousHealth = CurrentHealth;
        // m_PreviousOpponentHealth = target.CurrentHealth;

        // EndEpisode
        if (m_IsCollisionWithWarship)
        {
            CurrentHealth = 0f;
            SetReward(0f);
            //target.SetReward(0f);
            //EndEpisode();
            //target.EndEpisode();
        }

        m_TaskForce.EnvController.NotifyAgentDestroyed();
    }

    // [Obsolete]
    public override void Heuristic(float[] actionsOut)
    {
        actionsOut[0] = (float) MoveCommand.IDLE;
        actionsOut[1] = (float) AttackCommand.IDLE;

        Warship target = Target;

        // FIXME
        if (target == null)
        {
            if (Engine.SteerLevel < 0)
            {
                actionsOut[0] = (float) MoveCommand.RIGHT;
            }
            else if (Engine.SteerLevel > 0)
            {
                actionsOut[0] = (float) MoveCommand.LEFT;
            }
            else if (Engine.SpeedLevel > 0)
            {
                actionsOut[0] = (float) MoveCommand.BACKWARD;
            }
            else if (Engine.SpeedLevel < 0)
            {
                actionsOut[0] = (float) MoveCommand.FORWARD;
            }
            return;
        }

        Vector3 heading = transform.rotation.eulerAngles;
        Vector3 opponentHeading = target.transform.rotation.eulerAngles;

        float angle = (heading.y - opponentHeading.y + 360f) % 360f;    // (heading.y - opponentHeading.y) % 180f;

        ///
        const float radius = 100f;

        Vector3 position = transform.position;
        Vector3 targetPosition = target.transform.position;
        Vector3 positionGapVector = position - targetPosition;
        float gradient = positionGapVector.z / positionGapVector.x;
        float x = Mathf.Sqrt(Mathf.Pow(radius, 2) / (Mathf.Pow(gradient, 2) + 1));
        float z = gradient * x;

        float distancePositive = Geometry.GetDistance(position, targetPosition + new Vector3(x, 0f, z));
        float distanceNegative = Geometry.GetDistance(position, targetPosition - new Vector3(x, 0f, z));
        Vector3 nextReachPosition = targetPosition - Mathf.Sign(distancePositive - distanceNegative) * new Vector3(x, 0f, z);
        Vector3 nextReachDirection = nextReachPosition - position;

        // Raycast Detection
        RaycastHit hit;
        Vector3 forceRepulsive = Vector3.zero;
        for (int i = 0; i < 8; i++)
        {
            m_RaycastHitDistances[i] = 1.0f;

            float rad = (45f * i + transform.rotation.eulerAngles.y) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            if (Physics.Raycast(position, dir, out hit, maxDistance: 400f))
            {
                if (hit.collider.tag == "Terrain" && hit.distance < 20f)
                {
                    forceRepulsive += (position - hit.point) * 2f;
                }
                else if (hit.collider.tag == "Player")
                {
                    forceRepulsive += (position - hit.point) * Mathf.Pow(800f / (position - hit.point).magnitude, 2f);
                }

                m_RaycastHitDistances[i] = hit.distance / (BattleField.localScale.x * 2);
            }
        }

        nextReachDirection = (nextReachDirection.normalized + forceRepulsive.normalized) * nextReachDirection.magnitude;
        nextReachPosition = position + nextReachDirection;

        if (Engine.SpeedLevel < 2)
        {
            actionsOut[0] = (float) MoveCommand.FORWARD;
            return;
        }

        float y = Geometry.GetAngleBetween(transform.position, nextReachPosition);
        float ydir = (transform.rotation.eulerAngles.y - y + 180f) % 360f - 180f;
        if (ydir > 3f)
        {
            actionsOut[0] = (float) MoveCommand.LEFT;
        }
        else if (ydir < -3f)
        {
            actionsOut[0] = (float) MoveCommand.RIGHT;
        }

        // if (m_AimingPoint.x < 1f)
        // {
        //     actionsOut[1] = 3f;
        // }
        // else
        float distance = Vector3.Distance(position, targetPosition);
        if (distance < Turret.AttackRange)
        {
            actionsOut[1] = (float) AttackCommand.FIRE;
        }
        // if (distance < Turret.AttackRange)
        // {
        //     if (distance < Turret.AttackRange / 2)
        //     {
        //         if (m_AimingPoint.x < 1f)
        //         {
        //             actionsOut[1] = (float) AttackCommand.PITCH_DOWN;
        //         }
        //         else if (m_AimingPoint.x > 1f)
        //         {
        //             actionsOut[1] = (float) AttackCommand.PITCH_UP;
        //         }
        //         else
        //         {
        //             actionsOut[1] = (float) AttackCommand.FIRE;
        //         }

        //         return;
        //     }

        //     if (m_AimingPoint.x > 0f)
        //     {
        //         actionsOut[1] = (float) AttackCommand.PITCH_UP;
        //     }
        //     else if (m_AimingPoint.x < 0f)
        //     {
        //         actionsOut[1] = (float) AttackCommand.PITCH_DOWN;
        //     }
        //     else
        //     {
        //         actionsOut[1] = (float) AttackCommand.FIRE;
        //     }
        // }
    }
    #endregion  // MLAgent
// #endif

    public void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.name.StartsWith("Water"))
        {
            return;
        }

        //Debug.Log($"Warship({name}/{TeamId}-{PlayerId}).OnCollisionEnter(collision: {collision.collider.name}/{collision.collider.tag})");

        explosion.transform.position = collision.transform.position;
        explosion.transform.rotation = collision.transform.rotation;
        explosion.Play();

        // Debug.Log($"Warship.OnCollisionEnter: {tag}/{name} -> {collision.collider.tag} ({collision.collider.name}) (== Bullet: {collision.collider.tag.StartsWith("Bullet")})");

        if (collision.collider.tag == "Player")
        {
            m_IsCollisionWithWarship = true;
        }
        else if (collision.collider.tag == "Torpedo")
        {
            CurrentHealth = 0f;
        }
        else if (collision.collider.tag == "Bullet")
        {
            Shell shell = collision.collider.GetComponent<Shell>();
            if (shell.Warship.TeamId != TeamId)
            {
                OnDamageTaken();
            }
            //
        }

        // else if (collision.collider.tag.StartsWith("Bullet")
        //          && !collision.collider.tag.EndsWith(TeamId.ToString()))
        // {
        //     //float damage = collision.rigidbody?.velocity.magnitude ?? 20f;
        //     //CurrentHealth -= damage;
        //     OnDamageTaken();
        // }

        // else if (collision.collider.tag == "Terrain")
        // {
        //     //float damage = rb.velocity.magnitude * rb.mass;
        //     //CurrentHealth -= damage;
        //     CurrentHealth = 0;
        // }
    }

    public void OnTriggerEnter(Collider other)
    {
        //explosion.transform.position = other.transform.position;
        //explosion.transform.rotation = other.transform.rotation;
        //explosion.Play();
        // explosion.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        //Debug.Log($"Warship({name}/{TeamId}-{PlayerId}).OnTriggerEnter(Collider: {other.name}/{other.tag})");
        //StartCoroutine(DisplayExplosionEffect(other));
    }

    // private IEnumerator DisplayExplosionEffect(Collider other)
    // {
    //     explosion.transform.position = other.transform.position;
    //     explosion.transform.rotation = other.transform.rotation;
    //     explosion.Play();

    //     yield return new WaitForSeconds(2f);

    //     explosion.Stop(true, ParticleSystemStopBehavior.StopEmitting);

    //     yield break;
    // }

    public void OnDamageTaken()
    {
        AccumulatedDamage += 1f;
    }

    public WarshipModel GenerateWarshipModel()
    {
        return new WarshipModel()
        {
            TeamId = this.TeamId,
            CurrentHealth = this.CurrentHealth,
            IsDestroyed = this.IsDestroyed,
            IsDetected = this.IsDetected,
            Position = this.transform.position,
            Rotation = this.transform.rotation,
            Fuel = this.Engine.Fuel
        };
    }

    private void AddWarshipObservation(VectorSensor sensor, WarshipModel warship, int teamId = 1)    // (9)
    {
        sensor.AddObservation(warship.CurrentHealth / (float) k_MaxHealth);
        sensor.AddObservation(warship.TeamId == this.TeamId);
        sensor.AddObservation(warship.IsDestroyed);
        sensor.AddObservation(warship.IsDetected); // FIXME: Last Observation Model

        // TODO: Relative Position
        Vector2 position = new Vector2(warship.Position.x / BattleField.transform.localScale.x,
                                       warship.Position.z / BattleField.transform.localScale.z);
        sensor.AddObservation(position);

        float radian = (warship.Rotation.eulerAngles.y % 360) * Mathf.Deg2Rad;
        sensor.AddObservation(Mathf.Cos(radian));
        sensor.AddObservation(Mathf.Sin(radian));

        sensor.AddObservation(warship.Fuel / Engine.maxFuel);
    }

    private void AddDominantObservation(VectorSensor sensor)
    {
        // TODO
    }

    private void AddTorpedoObservation(VectorSensor sensor)
    {
        // Torpedo
        // bool isEnemyTorpedoLaunched = false;
        // Vector3 enemyTorpedoPosition = Vector3.zero;
        // GameObject torpedo = target.weaponSystemsOfficer.torpedoInstance;
        // if (torpedo != null)
        // {
        //     isEnemyTorpedoLaunched = true;
        //     enemyTorpedoPosition = torpedo.transform.position;
        // }
        //sensor.AddObservation(isEnemyTorpedoLaunched);
        //sensor.AddObservation(enemyTorpedoPosition.x / (BattleField.transform.localScale.x / 2) - 1f);
        //sensor.AddObservation(enemyTorpedoPosition.z / (BattleField.transform.localScale.z / 2) - 1f);
        //sensor.AddObservation(weaponSystemsOfficer.isTorpedoReady);
        //sensor.AddObservation(weaponSystemsOfficer.torpedoCooldown / WeaponSystemsOfficer.m_TorpedoReloadTime);
    }

    private void ShapeReward()
    {
        // FIXME: Group
        // else if (Engine.Fuel <= 0f + Mathf.Epsilon
        //          || weaponSystemsOfficer.Ammo == 0)
        // {
        //     // Time-out
        //     if (CurrentHealth > target.CurrentHealth)
        //     {
        //         SetReward(1f);
        //         target.SetReward(-1f);
        //         //EndEpisode();
        //         //target.EndEpisode();
        //     }
        //     else if (CurrentHealth < target.CurrentHealth)
        //     {
        //         SetReward(-1f);
        //         target.SetReward(1f);
        //         //EndEpisode();
        //         //target.EndEpisode();
        //     }
        //     else
        //     {
        //         SetReward(0f);
        //         target.SetReward(0f);
        //         //EndEpisode();
        //         //target.EndEpisode();
        //     }
        // }
        // else if (CurrentHealth <= 0f + Mathf.Epsilon)
        // {
        //     SetReward(-1f);
        //     target.SetReward(1f);
        //     //EndEpisode();
        //     //target.EndEpisode();
        // }
        // else if (target.CurrentHealth <= 0f + Mathf.Epsilon)
        // {
        //     SetReward(1f);
        //     target.SetReward(-1f);
        //     //EndEpisode();
        //     //target.EndEpisode();
        // }
    }
}

public class WarshipModel
{
    public int TeamId;
    // public int PlayerId;
    public float CurrentHealth;
    public bool IsDestroyed;
    public bool IsDetected;
    public Vector3 Position;
    public Quaternion Rotation;
    public float Fuel;
}
*/
