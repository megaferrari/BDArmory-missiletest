using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using BDArmory.Control;
using BDArmory.Extensions;
using BDArmory.FX;
using BDArmory.Guidances;
using BDArmory.Radar;
using BDArmory.Settings;
using BDArmory.Targeting;
using BDArmory.UI;
using BDArmory.Utils;
using BDArmory.WeaponMounts;
using BDArmory.Bullets;
using BDArmory.CounterMeasure;


namespace BDArmory.Weapons.Missiles
{
    public class MissileLauncher : MissileBase, IPartMassModifier
    {
        public Coroutine reloadRoutine;
        Coroutine reloadableMissile;
        #region Variable Declarations

        [KSPField]
        public string homingType = "AAM";

        [KSPField]
        public float guidanceDelay = -1;

        [KSPField]
        public float pronavGain = 3f;

        [KSPField]
        public float gLimit = -1;

        [KSPField]
        public float gMargin = -1;

        [KSPField]
        public string targetingType = "none";

        [KSPField]
        public string antiradTargetTypes = "0,5";

        public MissileTurret missileTurret = null;
        public BDRotaryRail rotaryRail = null;
        public BDDeployableRail deployableRail = null;
        public MultiMissileLauncher multiLauncher = null;
        private BDStagingAreaGauge gauge;
        private float reloadTimer = 0;
        public float heatTimer = -1;
        private Vector3 origScale = Vector3.one;

        #region Effects

        // Classic FX

        [KSPField]
        public string exhaustPrefabPath;

        [KSPField]
        public string boostExhaustPrefabPath;

        [KSPField]
        public string boostExhaustTransformName;

        #endregion

        #region Aero

        [KSPField]
        public bool aero = false;

        [KSPField]
        public string liftArea = "0.015";
        private float[] parsedLiftArea;
        public float currLiftArea = 0.015f;

        [KSPField]
        public string dragArea = "-1"; // Optional parameter to specify separate drag reference area, otherwise defaults to liftArea
        private float[] parsedDragArea;
        public float currDragArea = -1f;

        [KSPField]
        public string steerMult = "0.5";
        private float[] parsedSteerMult;
        public float currSteerMult = 0.5f;

        [KSPField]
        public float torqueRampUp = 30f;
        Vector3 aeroTorque = Vector3.zero;
        float controlAuthority;
        float finalMaxTorque;

        [KSPField]
        public float aeroSteerDamping = 0;

        [KSPField]
        public string maxTorqueAero = "0";
        private float[] parsedMaxTorqueAero;
        public float currMaxTorqueAero = 0f;

        #endregion Aero

        [KSPField]
        public string maxTorque = "90";
        private float[] parsedMaxTorque;
        public float currMaxTorque = 90;

        [KSPField]
        public float thrust = 30;

        [KSPField]
        public float cruiseThrust = 3;

        [KSPField]
        public float boostTime = 2.2f;

        [KSPField]
        public float cruiseTime = 45;

        [KSPField]
        public float cruiseDelay = 0;

        [KSPField]
        public float cruiseRangeTrigger = -1;

        [KSPField]
        public float maxAoA = 35;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "#LOC_BDArmory_Direction"),//Direction: 
            UI_Toggle(disabledText = "#LOC_BDArmory_Direction_disabledText", enabledText = "#LOC_BDArmory_Direction_enabledText")]//Lateral--Forward
        public bool decoupleForward = false;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_DecoupleSpeed"),//Decouple Speed
                  UI_FloatRange(minValue = 0f, maxValue = 10f, stepIncrement = 0.1f, scene = UI_Scene.Editor)]
        public float decoupleSpeed = 0;

        [KSPField]
        public float clearanceRadius = 0.14f;

        public override float ClearanceRadius => clearanceRadius;

        [KSPField]
        public float clearanceLength = 0.14f;

        public override float ClearanceLength => clearanceLength;

        [KSPField]
        public float optimumAirspeed = 220;

        [KSPField]
        public FloatCurve pronavGainCurve = new FloatCurve();

        [KSPField]
        public float blastRadius = -1;

        [KSPField]
        public float blastPower = 0; // Depreciated, support for legacy missiles only

        [KSPField]
        public float blastHeat = -1;

        [KSPField]
        public float maxTurnRateDPS = 20;

        [KSPField]
        public bool proxyDetonate = true;

        [KSPField]
        public string audioClipPath = string.Empty;

        AudioClip thrustAudio;

        [KSPField]
        public string boostClipPath = string.Empty;

        AudioClip boostAudio;

        [KSPField]
        public bool isSeismicCharge = false;

        [KSPField]
        public float rndAngVel = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxAltitude"),//Max Altitude
         UI_FloatRange(minValue = 0f, maxValue = 5000f, stepIncrement = 10f, scene = UI_Scene.All)]
        public float maxAltitude = 0f;

        [KSPField]
        public string rotationTransformName = string.Empty;
        Transform rotationTransform;

        [KSPField]
        public string terminalGuidanceType = "";

        [KSPField]
        public bool dumbTerminalGuidance = true;

        [KSPField]
        public float terminalGuidanceDistance = 0.0f;

        private bool terminalGuidanceActive;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_TerminalGuidance"), UI_Toggle(disabledText = "#LOC_BDArmory_false", enabledText = "#LOC_BDArmory_true")]//Terminal Guidance: false true
        public bool terminalGuidanceShouldActivate = true;

        [KSPField]
        public string explModelPath = "BDArmory/Models/explosion/explosion";

        public string explSoundPath = "BDArmory/Sounds/explode1";

        //weapon specifications
        [KSPField(advancedTweakable = true, isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "#LOC_BDArmory_FiringPriority"),
            UI_FloatRange(minValue = 0, maxValue = 10, stepIncrement = 1, scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public float priority = 0; //per-weapon priority selection override

        [KSPField]
        public bool spoolEngine = false;

        [KSPField]
        public bool hasRCS = false;

        [KSPField]
        public float rcsThrust = 1;
        float rcsRVelThreshold = 0.13f;
        KSPParticleEmitter upRCS;
        KSPParticleEmitter downRCS;
        KSPParticleEmitter leftRCS;
        KSPParticleEmitter rightRCS;
        List<KSPParticleEmitter> forwardRCS;
        float rcsAudioMinInterval = 0.2f;

        private AudioSource audioSource;
        public AudioSource sfAudioSource;
        List<KSPParticleEmitter> pEmitters;
        List<BDAGaplessParticleEmitter> gaplessEmitters;

        //float cmTimer;

        //deploy animation
        [KSPField]
        public string deployAnimationName = "";

        [KSPField]
        public bool deployedLiftInCruise = true;

        [KSPField]
        public float deployedDrag = 0.02f;

        [KSPField]
        public float deployTime = 0.2f;

        [KSPField]
        public string cruiseAnimationName = "";

        [KSPField]
        public float cruiseDeployTime = 0.2f;

        [KSPField]
        public string flightAnimationName = "";

        [KSPField]
        public bool OneShotAnim = true;

        [KSPField]
        public bool useSimpleDrag = false;

        public bool useSimpleDragTemp = false;

        [KSPField]
        public float simpleDrag = 0.02f;

        [KSPField]
        public float simpleStableTorque = 5;

        [KSPField]
        public Vector3 simpleCoD = new Vector3(0, 0, -1);

        [KSPField]
        public float agmDescentRatio = 1.45f;

        float currentThrust;

        public bool deployed;
        //public float deployedTime;

        AnimationState[] deployStates;

        AnimationState[] cruiseStates;

        AnimationState[] animStates;

        bool hasPlayedFlyby;

        float debugTurnRate;

        List<GameObject> boosters;

        List<GameObject> fairings;

        [KSPField]
        public bool decoupleBoosters = false;
        bool boostersDecoupled = false;

        [KSPField]
        public float boosterDecoupleSpeed = 5;

        [KSPField]
        public float boosterMass = 0; // The booster mass (dry mass if using fuel, wet otherwise)

        //Fuel Weight variables
        [KSPField]
        public float boosterFuelMass = 0; // The mass of the booster fuel (separate from the booster mass)

        [KSPField]
        public float cruiseFuelMass = 0; // The mass of the cruise fuel

        [KSPField]
        public bool useFuel = false;

        Transform vesselReferenceTransform;

        [KSPField]
        public string boostTransformName = string.Empty;
        List<KSPParticleEmitter> boostEmitters;
        List<BDAGaplessParticleEmitter> boostGaplessEmitters;

        [KSPField]
        public string fairingTransformName = string.Empty;

        [KSPField]
        public bool torpedo = false;

        [KSPField]
        public float waterImpactTolerance = 25;

        //ballistic options
        [KSPField]
        public bool indirect = false; //unused

        [KSPField]
        public bool vacuumSteerable = true;

        // Loft Options
        [KSPField]
        public string terminalHomingType = "pronav";

        [KSPField]
        public float LoftTermRange = -1;

        public GPSTargetInfo designatedGPSInfo;

        float[] rcsFiredTimes;
        KSPParticleEmitter[] rcsTransforms;

        private bool OldInfAmmo = false;
        private bool StartSetupComplete = false;

        //Fuel Burn Variables
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => -burnedFuelMass - (boostersDecoupled ? boosterMass : 0);
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;

        private float burnRate = 0;
        private float burnedFuelMass = 0;
        private float maxCruiseSpeed = 300f;

        private int cruiseTerminationFrames = 0;

        public bool SetupComplete => StartSetupComplete;
        public int[] torqueBounds = [-1,7];
        public float[] torqueAoABounds = [-1f, -1f, -1f];
        public SmoothingF smoothedAoA;
        #endregion Variable Declarations

        [KSPAction("Fire Missile")]
        public void AGFire(KSPActionParam param)
        {
            if (BDArmorySetup.Instance.ActiveWeaponManager != null && BDArmorySetup.Instance.ActiveWeaponManager.vessel == vessel) BDArmorySetup.Instance.ActiveWeaponManager.SendTargetDataToMissile(this, null);
            if (missileTurret)
            {
                missileTurret.FireMissile(this, null);
            }
            else if (rotaryRail)
            {
                rotaryRail.FireMissile(this, null);
            }
            else if (deployableRail)
            {
                deployableRail.FireMissile(this, null);
            }
            else
            {
                FireMissile();
            }
            if (BDArmorySetup.Instance.ActiveWeaponManager != null) BDArmorySetup.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPEvent(guiActive = true, guiName = "#LOC_BDArmory_FireMissile", active = true)]//Fire Missile
        public void GuiFire()
        {
            if (BDArmorySetup.Instance.ActiveWeaponManager != null && BDArmorySetup.Instance.ActiveWeaponManager.vessel == vessel) BDArmorySetup.Instance.ActiveWeaponManager.SendTargetDataToMissile(this, null);
            if (missileTurret)
            {
                missileTurret.FireMissile(this, null);
            }
            else if (rotaryRail)
            {
                rotaryRail.FireMissile(this, null);
            }
            else if (deployableRail)
            {
                deployableRail.FireMissile(this, null);
            }
            else
            {
                FireMissile();
            }
            if (BDArmorySetup.Instance.ActiveWeaponManager != null) BDArmorySetup.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, active = true, guiName = "#LOC_BDArmory_Jettison")]//Jettison
        public override void Jettison()
        {
            if (missileTurret) return;
            if (multiLauncher && !multiLauncher.permitJettison) return;
            part.decouple(0);
            if (BDArmorySetup.Instance.ActiveWeaponManager != null) BDArmorySetup.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPAction("Jettison")]
        public void AGJettsion(KSPActionParam param)
        {
            Jettison();
        }

        void ParseWeaponClass()
        {
            missileType = missileType.ToLower();
            if (missileType == "bomb")
            {
                weaponClass = WeaponClasses.Bomb;
            }
            else if (missileType == "torpedo" || missileType == "depthcharge")
            {
                weaponClass = WeaponClasses.SLW;
            }
            else
            {
                weaponClass = WeaponClasses.Missile;
            }
        }

        public override void OnStart(StartState state)
        {
            //base.OnStart(state);

            if (useFuel)
            {
                float initialMass = part.mass;
                if (boosterFuelMass < 0 || boostTime <= 0)
                {
                    if (boosterFuelMass < 0) Debug.LogWarning($"[BDArmory.MissileLauncher]: Error in configuration of {part.name}, boosterFuelMass: {boosterFuelMass} can't be less than 0, reverting to default value.");
                    boosterFuelMass = 0;
                }

                if (cruiseFuelMass < 0 || cruiseTime <= 0)
                {
                    if (cruiseFuelMass < 0) Debug.LogWarning($"[BDArmory.MissileLauncher]: Error in configuration of {part.name}, cruiseFuelMass: {cruiseFuelMass} can't be less than 0, reverting to default value.");
                    cruiseFuelMass = 0;
                }

                if (boosterMass + boosterFuelMass + cruiseFuelMass > initialMass * 0.95f)
                {
                    Debug.LogWarning($"[BDArmory.MissileLauncher]: Error in configuration of {part.name}, boosterMass: {boosterMass} + boosterFuelMass: {boosterFuelMass} + cruiseFuelMass: {cruiseFuelMass} can't be greater than 95% of the missile mass {initialMass}, clamping to 80% of the missile mass.");
                    if (boosterFuelMass > 0 || boostTime > 0)
                    {
                        if (cruiseFuelMass > 0 || cruiseTime > 0)
                        {
                            var totalBoosterMass = Mathf.Clamp(boosterMass + boosterFuelMass, 0, initialMass * 0.4f); // Scale total booster mass + fuel to 40% of missile.
                            boosterMass = boosterMass / (boosterMass + boosterFuelMass) * totalBoosterMass;
                            boosterFuelMass = totalBoosterMass - boosterMass;
                            cruiseFuelMass = Mathf.Clamp(cruiseFuelMass, 0, initialMass * 0.4f);
                        }
                        else
                        {
                            var totalBoosterMass = Mathf.Clamp(boosterMass + boosterFuelMass, 0, initialMass * 0.8f); // Scale total booster mass + fuel to 80% of missile.
                            boosterMass = boosterMass / (boosterMass + boosterFuelMass) * totalBoosterMass;
                            boosterFuelMass = totalBoosterMass - boosterMass;
                        }
                    }
                    else
                    {
                        boosterMass = 0; // Fuel-less boosters aren't sensible when requiring fuel.
                        cruiseFuelMass = Mathf.Clamp(cruiseFuelMass, 0, initialMass * 0.8f);
                    }
                }
                else
                {
                    if (boostTime > 0 && boosterFuelMass <= 0) boosterFuelMass = initialMass * 0.1f;
                    if (cruiseTime > 0 && cruiseFuelMass <= 0) cruiseFuelMass = initialMass * 0.1f;
                }
            }

            if (hasGimbal && maxSeekerGimbal < maxOffBoresight)
            {
                Debug.LogWarning($"[BDArmory.MissileLauncher]: Error in configuration of {part.name}, maxSeekerGimbal:{maxSeekerGimbal} can't be smaller than maxOffBoresight:{maxOffBoresight}, clamping to maxOffBoresight.");
                maxSeekerGimbal = maxOffBoresight;
            }

            if (shortName == string.Empty)
            {
                shortName = part.partInfo.title;
            }
            gaplessEmitters = new List<BDAGaplessParticleEmitter>();
            pEmitters = new List<KSPParticleEmitter>();
            boostEmitters = new List<KSPParticleEmitter>();
            boostGaplessEmitters = new List<BDAGaplessParticleEmitter>();
            if (hasRCS) forwardRCS = new List<KSPParticleEmitter>();

            Fields["maxOffBoresight"].guiActive = false;
            Fields["maxOffBoresight"].guiActiveEditor = false;
            if (missileFireAngle < 0 && maxOffBoresight < 360 && missileType.ToLower() == "missile" || missileType.ToLower() == "torpedo")
            {
                UI_FloatRange mFA = (UI_FloatRange)Fields["missileFireAngle"].uiControlEditor;
                mFA.maxValue = maxOffBoresight * 0.75f;
                //mFA.stepIncrement = mFA.maxValue / 100;
                missileFireAngle = maxOffBoresight * 0.75f;
            }

            Fields["maxStaticLaunchRange"].guiActive = false;
            Fields["maxStaticLaunchRange"].guiActiveEditor = false;
            Fields["minStaticLaunchRange"].guiActive = false;
            Fields["minStaticLaunchRange"].guiActiveEditor = false;

            ParseLiftDragSteerTorque();

            MissileGuidance.setupTorqueAoALimit(this, currLiftArea, currDragArea);

            loftState = LoftStates.Boost;
            TimeToImpact = float.PositiveInfinity;
            WeaveOffset = -1f;
            terminalHomingActive = false;

            if (LoftTermRange > 0)
            {
                Debug.LogWarning($"[BDArmory.MissileLauncher]: Error in configuration of {part.name}, LoftTermRange is deprecated, please use terminalHomingRange instead.");
                terminalHomingRange = LoftTermRange;
                LoftTermRange = -1;
            }
            // extension for feature_engagementenvelope

            using (var pEemitter = part.FindModelComponents<KSPParticleEmitter>().GetEnumerator())
                while (pEemitter.MoveNext())
                {
                    if (pEemitter.Current == null) continue;
                    EffectBehaviour.AddParticleEmitter(pEemitter.Current);
                    pEemitter.Current.emit = false;
                }

            if (HighLogic.LoadedSceneIsFlight)
            {
                missileName = part.name;

                if (warheadType == WarheadTypes.Standard || warheadType == WarheadTypes.ContinuousRod)
                {
                    var tnt = part.FindModuleImplementing<BDExplosivePart>();
                    if (tnt is null)
                    {
                        tnt = (BDExplosivePart)part.AddModule("BDExplosivePart");
                        tnt.tntMass = BlastPhysicsUtils.CalculateExplosiveMass(blastRadius);
                    }

                    //New Explosive module
                    DisablingExplosives(part);
                    if (tnt.explModelPath == ModuleWeapon.defaultExplModelPath) tnt.explModelPath = explModelPath; // If the BDExplosivePart is using the default explosion part and sound,
                    if (tnt.explSoundPath == ModuleWeapon.defaultExplSoundPath) tnt.explSoundPath = explSoundPath; // override them with those of the MissileLauncher (if specified).
                }

                MissileReferenceTransform = part.FindModelTransform("missileTransform");
                if (!MissileReferenceTransform)
                {
                    MissileReferenceTransform = part.partTransform;
                }

                origScale = part.partTransform.localScale;
                gauge = (BDStagingAreaGauge)part.AddModule("BDStagingAreaGauge");
                part.force_activate();

                if (!string.IsNullOrEmpty(exhaustPrefabPath))
                {
                    using (var t = part.FindModelTransforms("exhaustTransform").AsEnumerable().GetEnumerator())
                        while (t.MoveNext())
                        {
                            if (t.Current == null) continue;
                            AttachExhaustPrefab(exhaustPrefabPath, this, t.Current);
                        }
                }

                if (!string.IsNullOrEmpty(boostExhaustPrefabPath) && !string.IsNullOrEmpty(boostExhaustTransformName))
                {
                    using (var t = part.FindModelTransforms(boostExhaustTransformName).AsEnumerable().GetEnumerator())
                        while (t.MoveNext())
                        {
                            if (t.Current == null) continue;
                            AttachExhaustPrefab(boostExhaustPrefabPath, this, t.Current);
                        }
                }

                boosters = new List<GameObject>();
                if (!string.IsNullOrEmpty(boostTransformName))
                {
                    using (var t = part.FindModelTransforms(boostTransformName).AsEnumerable().GetEnumerator())
                        while (t.MoveNext())
                        {
                            if (t.Current == null) continue;
                            boosters.Add(t.Current.gameObject);
                            using (var be = t.Current.GetComponentsInChildren<KSPParticleEmitter>().AsEnumerable().GetEnumerator())
                                while (be.MoveNext())
                                {
                                    if (be.Current == null) continue;
                                    if (be.Current.useWorldSpace)
                                    {
                                        var existingBE = be.Current.GetComponent<BDAGaplessParticleEmitter>();
                                        if (existingBE)
                                        {
                                            existingBE.emit = false;
                                            be.Current.emit = false;
                                            continue;
                                        }
                                        BDAGaplessParticleEmitter ge = be.Current.gameObject.AddComponent<BDAGaplessParticleEmitter>();
                                        ge.part = part;
                                        ge.emit = false;
                                        boostGaplessEmitters.Add(ge);
                                    }
                                    else
                                    {
                                        if (!boostEmitters.Contains(be.Current))
                                        {
                                            boostEmitters.Add(be.Current);
                                        }
                                        EffectBehaviour.AddParticleEmitter(be.Current);
                                    }
                                }
                        }
                }

                fairings = new List<GameObject>();
                if (!string.IsNullOrEmpty(fairingTransformName))
                {
                    using (var t = part.FindModelTransforms(fairingTransformName).AsEnumerable().GetEnumerator())
                        while (t.MoveNext())
                        {
                            if (t.Current == null) continue;
                            fairings.Add(t.Current.gameObject);
                        }
                }

                using (var pEmitter = part.FindModelComponents<KSPParticleEmitter>().AsEnumerable().GetEnumerator())
                    while (pEmitter.MoveNext())
                    {
                        if (pEmitter.Current == null) continue;
                        var existingGE = pEmitter.Current.GetComponent<BDAGaplessParticleEmitter>();
                        if (existingGE || boostEmitters.Contains(pEmitter.Current))
                        {
                            if (existingGE) existingGE.emit = false;
                            continue;
                        }

                        if (pEmitter.Current.useWorldSpace)
                        {
                            BDAGaplessParticleEmitter gaplessEmitter = pEmitter.Current.gameObject.AddComponent<BDAGaplessParticleEmitter>();
                            gaplessEmitter.part = part;
                            gaplessEmitter.emit = false;
                            gaplessEmitters.Add(gaplessEmitter);
                        }
                        else
                        {
                            if (pEmitter.Current.transform.name != boostTransformName)
                            {
                                pEmitters.Add(pEmitter.Current);
                            }
                            else
                            {
                                boostEmitters.Add(pEmitter.Current);
                            }
                            EffectBehaviour.AddParticleEmitter(pEmitter.Current);
                        }
                    }

                using (IEnumerator<Light> light = gameObject.GetComponentsInChildren<Light>().AsEnumerable().GetEnumerator())
                    while (light.MoveNext())
                    {
                        if (light.Current == null) continue;
                        light.Current.intensity = 0;
                    }

                //cmTimer = Time.time;

                using (var pe = pEmitters.GetEnumerator())
                    while (pe.MoveNext())
                    {
                        if (pe.Current == null) continue;
                        if (hasRCS)
                        {
                            if (pe.Current.gameObject.name == "rcsUp") upRCS = pe.Current;
                            else if (pe.Current.gameObject.name == "rcsDown") downRCS = pe.Current;
                            else if (pe.Current.gameObject.name == "rcsLeft") leftRCS = pe.Current;
                            else if (pe.Current.gameObject.name == "rcsRight") rightRCS = pe.Current;
                            else if (pe.Current.gameObject.name.Contains("rcsForward")) forwardRCS.Add(pe.Current);
                        }

                        if (!pe.Current.gameObject.name.Contains("rcs") && !pe.Current.useWorldSpace)
                        {
                            //pe.Current.sizeGrow = 99999;
                        }
                    }

                if (rotationTransformName != string.Empty)
                {
                    rotationTransform = part.FindModelTransform(rotationTransformName);
                }

                if (hasRCS)
                {
                    SetupRCS();
                    KillRCS();
                }
                SetupAudio();
                var missileSpawner = part.FindModuleImplementing<ModuleMissileRearm>();
                if (missileSpawner != null)
                {
                    reloadableRail = missileSpawner;
                    hasAmmo = true;
                }
            }

            if (deployAnimationName != "")
            {
                deployStates = GUIUtils.SetUpAnimation(deployAnimationName, part);
            }
            else
            {
                deployedDrag = simpleDrag;
            }
            if (cruiseAnimationName != "")
            {
                cruiseStates = GUIUtils.SetUpAnimation(cruiseAnimationName, part);
            }
            if (flightAnimationName != "")
            {
                animStates = GUIUtils.SetUpAnimation(flightAnimationName, part);
            }

            warheadType = WarheadTypes.Kinetic; // Default to Kinetic if no appropriate modules are found.
            foreach (var partModule in part.Modules)
            {
                if (partModule == null) continue;
                switch (partModule.moduleName)
                {
                    case "BDExplosivePart":
                        ((BDExplosivePart)partModule).ParseWarheadType();
                        if (((BDExplosivePart)partModule).warheadReportingName == "Continuous Rod")
                            if (warheadType == WarheadTypes.Custom)
                                warheadType = WarheadTypes.CustomContinuous;
                            else
                                warheadType = WarheadTypes.ContinuousRod;
                        else
                            if (warheadType == WarheadTypes.Custom)
                            warheadType = WarheadTypes.CustomStandard;
                        else
                            warheadType = WarheadTypes.Standard;
                        continue; //EMPs sometimes have BDExplosivePart modules for FX, so keep going
                    case "BDCustomWarhead":
                        if (warheadType == WarheadTypes.ContinuousRod)
                            warheadType = WarheadTypes.CustomContinuous;
                        else if (warheadType == WarheadTypes.Standard)
                            warheadType = WarheadTypes.CustomStandard;
                        else
                            warheadType = WarheadTypes.Custom;
                        continue;
                    case "ClusterBomb":
                        clusterbomb = ((ClusterBomb)partModule).submunitions.Count;
                        break; //CBs destroy the part on deployment, doesn't support other modules, break
                    case "MultiMissileLauncher":
                        if (!String.IsNullOrEmpty(((MultiMissileLauncher)partModule).subMunitionName))
                        {
                            //shouldn't have both MML and ClusterBomb/BDExplosivepart/ModuleEMP/BDModuleNuke on the same part; explosive would be on the submunition .cfg
                            //so instead need a check if the MML comes with a default ordnance, and see what it is to inherit stats.
                            using (var parts = PartLoader.LoadedPartsList.GetEnumerator())
                                while (parts.MoveNext())
                                {
                                    if (parts.Current == null) continue;
                                    if (parts.Current.partConfig == null || parts.Current.partPrefab == null) continue;
                                    if (parts.Current.partPrefab.partInfo.name != ((MultiMissileLauncher)partModule).subMunitionName) continue;
                                    foreach (var subModule in parts.Current.partPrefab.Modules)
                                    {
                                        if (subModule == null) continue;
                                        switch (subModule.moduleName)
                                        {
                                            case "BDExplosivePart":
                                                ((BDExplosivePart)subModule).ParseWarheadType();
                                                if (((BDExplosivePart)subModule).warheadReportingName == "Continuous Rod")
                                                    if (warheadType == WarheadTypes.Custom)
                                                        warheadType = WarheadTypes.CustomContinuous;
                                                    else
                                                        warheadType = WarheadTypes.ContinuousRod;
                                                else
                                                    if (warheadType == WarheadTypes.Custom)
                                                    warheadType = WarheadTypes.CustomStandard;
                                                else
                                                    warheadType = WarheadTypes.Standard;
                                                continue; //EMPs sometimes have BDExplosivePart modules for FX, so keep going
                                            case "BDCustomWarhead":
                                                if (warheadType == WarheadTypes.ContinuousRod)
                                                    warheadType = WarheadTypes.CustomContinuous;
                                                else if (warheadType == WarheadTypes.Standard)
                                                    warheadType = WarheadTypes.CustomStandard;
                                                else
                                                    warheadType = WarheadTypes.Custom;
                                                continue;
                                            case "ClusterBomb":
                                                clusterbomb = ((ClusterBomb)subModule).submunitions.Count; //No bomb check, since I guess you could have a missile with a clusterbomb module, for some reason...?
                                                if (clusterbomb > 1) clusterbomb *= (int)((MultiMissileLauncher)partModule).salvoSize;
                                                break;
                                            case "ModuleEMP":
                                                warheadType = WarheadTypes.EMP;
                                                StandOffDistance = ((ModuleEMP)subModule).proximity;
                                                break;
                                            case "BDModuleNuke":
                                                warheadType = WarheadTypes.Nuke;
                                                StandOffDistance = BDAMath.Sqrt(((BDModuleNuke)subModule).yield) * 500;
                                                break;
                                        }
                                    }
                                }
                        }
                        else
                        {
                            if (warheadType == WarheadTypes.Kinetic) warheadType = WarheadTypes.Launcher; //empty MultiMissile Launcher                            
                        }
                        break; //MMLs don't support other modules, break
                    case "ModuleEMP":
                        warheadType = WarheadTypes.EMP;
                        StandOffDistance = ((ModuleEMP)partModule).proximity;
                        break;
                    case "BDModuleNuke":
                        warheadType = WarheadTypes.Nuke;
                        StandOffDistance = BDAMath.Sqrt(((BDModuleNuke)partModule).yield) * 500;
                        break;
                    default:
                        continue;
                }
                break; // Break if a valid module is found.
            }
            if (warheadType == WarheadTypes.Kinetic && blastPower > 0) warheadType = WarheadTypes.Legacy;
            SetFields();
            smoothedAoA = new SmoothingF(Mathf.Exp(Mathf.Log(0.5f) * Time.fixedDeltaTime * 10f)); // Half-life of 0.1s.
            StartSetupComplete = true;

            //IRCCM sanity check
            if (IRCCM == IRCCMModes.gateWidth || IRCCM == IRCCMModes.SG)
            {
                DefaultFOV = lockedSensorFOV;
                if (gateWidth > lockedSensorFOV)
                {
                    Debug.LogWarning($"[BDArmory.MissileLauncher]: Error in configuration of {part.name}, gateWidth:{gateWidth} can't be larger than lockedSensorFOV:{lockedSensorFOV}, clamping to LockedsensorFOV - 0.1� {lockedSensorFOV - 0.1f}");
                    gateWidth = lockedSensorFOV - 0.1f;
                }
            }
            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher] Start() setup complete");
        }

        public void SetFields()
        {
            ParseWeaponClass();
            ParseModes();
            InitializeEngagementRange(minStaticLaunchRange, maxStaticLaunchRange);
            SetInitialDetonationDistance();
            uncagedLock = (allAspect) ? allAspect : uncagedLock;
            guidanceFailureRatePerFrame = (guidanceFailureRate >= 1) ? 1f : 1f - Mathf.Exp(Mathf.Log(1f - guidanceFailureRate) * Time.fixedDeltaTime); // Convert from per-second failure rate to per-frame failure rate

            if (isTimed)
            {
                Fields["detonationTime"].guiActive = true;
                Fields["detonationTime"].guiActiveEditor = true;
            }
            else
            {
                Fields["detonationTime"].guiActive = false;
                Fields["detonationTime"].guiActiveEditor = false;
            }
            if (GuidanceMode != GuidanceModes.Cruise && (!terminalHoming || homingModeTerminal != GuidanceModes.Cruise))
            {
                CruiseAltitudeRange();
                Fields["CruiseAltitude"].guiActive = false;
                Fields["CruiseAltitude"].guiActiveEditor = false;
                Fields["CruiseSpeed"].guiActive = false;
                Fields["CruiseSpeed"].guiActiveEditor = false;
                Events["CruiseAltitudeRange"].guiActive = false;
                Events["CruiseAltitudeRange"].guiActiveEditor = false;
                Fields["CruisePredictionTime"].guiActiveEditor = false;
                Fields["CruisePopup"].guiActive = false;
                Fields["CruisePopup"].guiActiveEditor = false;
            }
            else
            {
                string maxCruiseSpeedString = ConfigNodeUtils.FindPartModuleConfigNodeValue(part.partInfo.partConfig, "MissileLauncher", "CruiseSpeed");
                if (!string.IsNullOrEmpty(maxCruiseSpeedString)) // Use the default value from the MM patch.
                {
                    try
                    {
                        maxCruiseSpeed = float.Parse(maxCruiseSpeedString);
                        if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher]: setting maxCruiseSpeed of " + part + " on " + part.vessel.vesselName + " to " + maxCruiseSpeed);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[BDArmory.MissileLauncher]: Failed to parse maxCruiseSpeed configNode: " + e.Message);
                    }
                }
                UI_FloatRange CruiseSpeedRange = (UI_FloatRange)Fields["CruiseSpeed"].uiControlEditor;
                CruiseSpeedRange.maxValue = maxCruiseSpeed;
                CruiseSpeedRange.stepIncrement = Mathf.Clamp((maxCruiseSpeed - 100f) * 0.1f, 5f, 50f);
                CruiseAltitudeRange();
                Fields["CruiseAltitude"].guiActive = true;
                Fields["CruiseAltitude"].guiActiveEditor = true;
                Fields["CruiseSpeed"].guiActive = true;
                Fields["CruiseSpeed"].guiActiveEditor = true;
                Events["CruiseAltitudeRange"].guiActive = true;
                Events["CruiseAltitudeRange"].guiActiveEditor = true;
                Fields["CruisePredictionTime"].guiActiveEditor = true;
                Fields["CruisePopup"].guiActive = true;
            }

            if (GuidanceMode != GuidanceModes.AGM)
            {
                Fields["maxAltitude"].guiActive = false;
                Fields["maxAltitude"].guiActiveEditor = false;
            }
            else
            {
                Fields["maxAltitude"].guiActive = true;
                Fields["maxAltitude"].guiActiveEditor = true;
            }
            if (GuidanceMode != GuidanceModes.AGMBallistic)
            {
                Fields["BallisticOverShootFactor"].guiActive = false;
                Fields["BallisticOverShootFactor"].guiActiveEditor = false;
                Fields["BallisticAngle"].guiActive = false;
                Fields["BallisticAngle"].guiActiveEditor = false;
            }
            else
            {
                Fields["BallisticOverShootFactor"].guiActive = true;
                Fields["BallisticOverShootFactor"].guiActiveEditor = true;
                Fields["BallisticAngle"].guiActive = true;
                Fields["BallisticAngle"].guiActiveEditor = true;
            }

            if (part.partInfo.title.Contains("Bomb") || weaponClass == WeaponClasses.SLW)
            {
                Fields["dropTime"].guiActive = false;
                Fields["dropTime"].guiActiveEditor = false;
                if (torpedo) dropTime = 0;
            }
            else
            {
                Fields["dropTime"].guiActive = true;
                Fields["dropTime"].guiActiveEditor = true;
            }

            if (TargetingModeTerminal != TargetingModes.None)
            {
                Fields["terminalGuidanceShouldActivate"].guiName += terminalGuidanceType;
            }
            else
            {
                Fields["terminalGuidanceShouldActivate"].guiActive = false;
                Fields["terminalGuidanceShouldActivate"].guiActiveEditor = false;
                terminalGuidanceShouldActivate = false;
            }

            if (GuidanceMode != GuidanceModes.AAMLoft && GuidanceMode != GuidanceModes.Kappa)
            {
                Fields["LoftMaxAltitude"].guiActive = false;
                Fields["LoftMaxAltitude"].guiActiveEditor = false;
                Fields["LoftRangeOverride"].guiActive = false;
                Fields["LoftRangeOverride"].guiActiveEditor = false;
                Fields["LoftAngle"].guiActive = false;
                Fields["LoftAngle"].guiActiveEditor = false;
                Fields["LoftTermAngle"].guiActive = false;
                Fields["LoftTermAngle"].guiActiveEditor = false;
                Fields["LoftRangeFac"].guiActive = false;
                Fields["LoftRangeFac"].guiActiveEditor = false;
                Fields["LoftVertVelComp"].guiActive = false;
                Fields["LoftVertVelComp"].guiActiveEditor = false;
            }
            else
            {
                
                Fields["LoftMaxAltitude"].guiActiveEditor = true;
                Fields["LoftRangeOverride"].guiActiveEditor = true;

                if (!GameSettings.ADVANCED_TWEAKABLES)
                {
                    Fields["LoftAngle"].guiActiveEditor = false;
                    Fields["LoftTermAngle"].guiActiveEditor = false;
                    Fields["LoftRangeFac"].guiActiveEditor = false;
                    Fields["LoftVertVelComp"].guiActiveEditor = false;
                }
                else
                {
                    Fields["LoftAngle"].guiActiveEditor = true;
                    Fields["LoftTermAngle"].guiActiveEditor = true;
                    Fields["LoftRangeFac"].guiActiveEditor = true;
                    Fields["LoftVertVelComp"].guiActiveEditor = true;
                }

                if (!BDArmorySettings.DEBUG_MISSILES)
                {
                    Fields["LoftMaxAltitude"].guiActive = false;
                    Fields["LoftRangeOverride"].guiActive = false;
                    Fields["LoftAngle"].guiActive = false;
                    Fields["LoftTermAngle"].guiActive = false;
                    Fields["LoftRangeFac"].guiActive = false;
                    Fields["LoftVertVelComp"].guiActive = false;
                }
                else
                {
                    Fields["LoftMaxAltitude"].guiActive = true;
                    Fields["LoftRangeOverride"].guiActive = true;
                    Fields["LoftAngle"].guiActive = true;
                    Fields["LoftTermAngle"].guiActive = true;
                    Fields["LoftRangeFac"].guiActive = true;
                    Fields["LoftVertVelComp"].guiActive = true;
                }
            }

            if (GuidanceMode != GuidanceModes.AAMLoft)
            {
                Fields["LoftMinAltitude"].guiActive = false;
                Fields["LoftMinAltitude"].guiActiveEditor = false;
                Fields["LoftVelComp"].guiActive = false;
                Fields["LoftVelComp"].guiActiveEditor = false;
                Fields["LoftVertVelComp"].guiActive = false;
                Fields["LoftVertVelComp"].guiActiveEditor = false;
                Fields["LoftAltitudeAdvMax"].guiActive = false;
                Fields["LoftAltitudeAdvMax"].guiActiveEditor = false;
                //Fields["LoftAltComp"].guiActive = false;
                //Fields["LoftAltComp"].guiActiveEditor = false;
                //Fields["terminalHomingRange"].guiActive = false;
                //Fields["terminalHomingRange"].guiActiveEditor = false;
            }
            else
            {
                Fields["LoftMinAltitude"].guiActiveEditor = true;
                Fields["LoftAltitudeAdvMax"].guiActiveEditor = true;
                //Fields["terminalHomingRange"].guiActive = true;
                //Fields["terminalHomingRange"].guiActiveEditor = true;

                if (!GameSettings.ADVANCED_TWEAKABLES)
                {
                    Fields["LoftVelComp"].guiActiveEditor = false;
                    Fields["LoftVertVelComp"].guiActiveEditor = false;
                    //Fields["LoftAltComp"].guiActive = false;
                    //Fields["LoftAltComp"].guiActiveEditor = false;
                }
                else
                {
                    Fields["LoftVelComp"].guiActiveEditor = true;
                    Fields["LoftVertVelComp"].guiActiveEditor = true;
                    //Fields["LoftAltComp"].guiActive = true;
                    //Fields["LoftAltComp"].guiActiveEditor = true;
                }

                if (!BDArmorySettings.DEBUG_MISSILES)
                {
                    Fields["LoftMinAltitude"].guiActive = false;
                    Fields["LoftAltitudeAdvMax"].guiActive = false;
                    Fields["LoftVelComp"].guiActive = false;
                    Fields["LoftVertVelComp"].guiActive = false;
                }
                else
                {
                    Fields["LoftMinAltitude"].guiActive = true;
                    Fields["LoftAltitudeAdvMax"].guiActive = true;
                    Fields["LoftVelComp"].guiActive = true;
                    Fields["LoftVertVelComp"].guiActive = true;
                }
            }
            if (!terminalHoming && GuidanceMode != GuidanceModes.AAMLoft) //(GuidanceMode != GuidanceModes.AAMHybrid && GuidanceMode != GuidanceModes.AAMLoft)
            {
                Fields["terminalHomingRange"].guiActive = false;
                Fields["terminalHomingRange"].guiActiveEditor = false;
            }
            else
            {
                Fields["terminalHomingRange"].guiActive = true;
                Fields["terminalHomingRange"].guiActiveEditor = true;
            }

            // fill lockedSensorFOVBias with default values if not set by part config:
            if ((TargetingMode == TargetingModes.Heat || TargetingModeTerminal == TargetingModes.Heat) && heatThreshold > 0 && lockedSensorFOVBias.minTime == float.MaxValue)
            {
                float a = lockedSensorFOV / 2f;
                float b = -1f * ((1f - 1f / 1.2f));
                float[] x = new float[6] { 0f * a, 0.2f * a, 0.4f * a, 0.6f * a, 0.8f * a, 1f * a };
                if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: OnStart missile {shortName}: setting default lockedSensorFOVBias curve to:");
                for (int i = 0; i < 6; i++)
                {
                    lockedSensorFOVBias.Add(x[i], b / (a * a) * x[i] * x[i] + 1f, -1f / 3f * x[i] / (a * a), -1f / 3f * x[i] / (a * a));
                    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("key = " + x[i] + " " + (b / (a * a) * x[i] * x[i] + 1f) + " " + (-1f / 3f * x[i] / (a * a)) + " " + (-1f / 3f * x[i] / (a * a)));
                }
            }

            // fill lockedSensorVelocityBias with default values if not set by part config:
            if ((TargetingMode == TargetingModes.Heat || TargetingModeTerminal == TargetingModes.Heat) && heatThreshold > 0)
            {
                bool defaultVelocityBias = false;
                if (lockedSensorVelocityBias.minTime == float.MaxValue)
                {
                    lockedSensorVelocityBias.Add(0f, 1f);
                    lockedSensorVelocityBias.Add(180f, 1f);
                    defaultVelocityBias = true;
                    if (BDArmorySettings.DEBUG_MISSILES)
                    {
                        Debug.Log($"[BDArmory.MissileLauncher]: OnStart missile {shortName}: setting default lockedSensorVelocityBias curve to:");
                        Debug.Log("key = 0 1");
                        Debug.Log("key = 180 1");
                    }
                }
                if (lockedSensorVelocityMagnitudeBias.minTime == float.MaxValue)
                {
                    lockedSensorVelocityMagnitudeBias.Add(1f, 1f);
                    if (defaultVelocityBias)
                        lockedSensorVelocityMagnitudeBias.Add(0f, 1f);
                    else
                        lockedSensorVelocityMagnitudeBias.Add(0f, 0f);
                    if (BDArmorySettings.DEBUG_MISSILES)
                    {
                        Debug.Log($"[BDArmory.MissileLauncher]: OnStart missile {shortName}: setting default lockedSensorVelocityMagnitudeBias curve to:");
                        Debug.Log("key = 1 1");
                        if (defaultVelocityBias)
                            Debug.Log("key = 0 1");
                        else
                            Debug.Log("key = 0 0");
                    }
                }
            }

            // fill activeRadarLockTrackCurve, activeRadarVelocityGate and activeRadarRangeGate with default values if not set by part config:
            if ((TargetingMode == TargetingModes.Radar || TargetingModeTerminal == TargetingModes.Radar) && activeRadarRange > 0)
            {
                if (activeRadarLockTrackCurve.minTime == float.MaxValue)
                {
                    activeRadarLockTrackCurve.Add(0f, 0f);
                    activeRadarLockTrackCurve.Add(activeRadarRange, RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS);           // TODO: tune & balance constants!
                    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: OnStart missile {shortName}: setting default locktrackcurve with maxrange/minrcs: {activeRadarLockTrackCurve.maxTime}/{RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS}");
                }

                if (activeRadarVelocityGate.minTime == float.MaxValue)
                {
                    activeRadarVelocityGate.Add(0f, RadarUtils.MISSILE_DEFAULT_GATE_RCS);
                    activeRadarVelocityGate.Add(activeRadarVelocityFilter, 1f);           // TODO: tune & balance constants!
                    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: OnStart missile {shortName}: setting default activeRadarVelocityGate with maxfilter: {activeRadarLockTrackCurve.maxTime}");
                }
                else
                {
                    activeRadarVelocityFilter = activeRadarVelocityGate.maxTime;
                }


                if (activeRadarRangeGate.minTime == float.MaxValue)
                {
                    activeRadarRangeGate.Add(0f, 1f);
                    activeRadarRangeGate.Add(activeRadarRangeFilter * 0.001f, 0f);           // TODO: tune & balance constants!
                    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: OnStart missile {shortName}: setting default activeRadarRangeGate with maxfilter/minrcs: {activeRadarRangeGate.maxTime}/{RadarUtils.MISSILE_DEFAULT_GATE_RCS}");
                }
                else
                {
                    activeRadarRangeFilter = activeRadarRangeGate.maxTime;
                }
            }

            // Don't show detonation distance settings for kinetic warheads
            if (warheadType == WarheadTypes.Kinetic)
            {
                Fields["DetonationDistance"].guiActive = false;
                Fields["DetonationDistance"].guiActiveEditor = false;
                Fields["DetonateAtMinimumDistance"].guiActive = false;
                Fields["DetonateAtMinimumDistance"].guiActiveEditor = false;
            }
            ParseAntiRadTargetTypes();
            GUIUtils.RefreshAssociatedWindows(part);
        }

        /// <summary>
        /// This method will convert the blastPower to a tnt mass equivalent
        /// </summary>
        private void FromBlastPowerToTNTMass()
        {
            blastPower = BlastPhysicsUtils.CalculateExplosiveMass(blastRadius);
        }

        void OnCollisionEnter(Collision col)
        {
            base.CollisionEnter(col);
        }

        void SetupAudio()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.minDistance = 1;
                audioSource.maxDistance = 1000;
                audioSource.loop = true;
                audioSource.pitch = 1f;
                audioSource.priority = 255;
                audioSource.spatialBlend = 1;
            }

            if (audioClipPath != string.Empty)
            {
                audioSource.clip = SoundUtils.GetAudioClip(audioClipPath);
            }

            if (sfAudioSource == null)
            {
                sfAudioSource = gameObject.AddComponent<AudioSource>();
                sfAudioSource.minDistance = 1;
                sfAudioSource.maxDistance = 2000;
                sfAudioSource.dopplerLevel = 0;
                sfAudioSource.priority = 230;
                sfAudioSource.spatialBlend = 1;
            }

            if (audioClipPath != string.Empty)
            {
                thrustAudio = SoundUtils.GetAudioClip(audioClipPath);
            }

            if (boostClipPath != string.Empty)
            {
                boostAudio = SoundUtils.GetAudioClip(boostClipPath);
            }

            UpdateVolume();
            BDArmorySetup.OnVolumeChange -= UpdateVolume; // Remove it if it's already there. (Doesn't matter if it isn't.)
            BDArmorySetup.OnVolumeChange += UpdateVolume;
        }

        void UpdateVolume()
        {
            if (audioSource)
            {
                audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            }
            if (sfAudioSource)
            {
                sfAudioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            }
        }

        void OnDestroy()
        {
            //Debug.Log("{TorpDebug] torpedo crash tolerance: " + part.crashTolerance);
            DetachExhaustPrefabs();
            KillRCS();
            if (upRCS) EffectBehaviour.RemoveParticleEmitter(upRCS);
            if (downRCS) EffectBehaviour.RemoveParticleEmitter(downRCS);
            if (leftRCS) EffectBehaviour.RemoveParticleEmitter(leftRCS);
            if (rightRCS) EffectBehaviour.RemoveParticleEmitter(rightRCS);
            if (forwardRCS != null)
                foreach (var pe in forwardRCS)
                    if (pe) EffectBehaviour.RemoveParticleEmitter(pe);
            if (pEmitters != null)
                foreach (var pe in pEmitters)
                    if (pe) EffectBehaviour.RemoveParticleEmitter(pe);
            if (gaplessEmitters is not null) // Make sure the gapless emitters get destroyed (they should anyway, but KSP holds onto part references, which may prevent this from happening automatically).
                foreach (var gpe in gaplessEmitters)
                    if (gpe is not null) Destroy(gpe);
            if (boostGaplessEmitters is not null) // Make sure the gapless emitters get destroyed (they should anyway, but KSP holds onto part references, which may prevent this from happening automatically).
                foreach (var bgpe in boostGaplessEmitters)
                    if (bgpe is not null) Destroy(bgpe);
            if (boostEmitters != null)
                foreach (var pe in boostEmitters)
                    if (pe) EffectBehaviour.RemoveParticleEmitter(pe);
            BDArmorySetup.OnVolumeChange -= UpdateVolume;
            GameEvents.onPartDie.Remove(PartDie);
            if (vesselReferenceTransform != null && vesselReferenceTransform.gameObject != null)
            {
                Destroy(vesselReferenceTransform.gameObject);
            }
        }

        public override float GetBlastRadius()
        {
            if (blastRadius >= 0) { return blastRadius; }
            else
            {
                if (warheadType == WarheadTypes.EMP)
                {
                    if (part.FindModuleImplementing<ModuleEMP>() != null)
                    {
                        blastRadius = part.FindModuleImplementing<ModuleEMP>().proximity;
                        return blastRadius;
                    }
                    else
                    {
                        blastRadius = 150;
                        return 150;
                    }
                }
                else if (warheadType == WarheadTypes.Nuke)
                {
                    if (part.FindModuleImplementing<BDModuleNuke>() != null)
                    {
                        blastRadius = BDAMath.Sqrt(part.FindModuleImplementing<BDModuleNuke>().yield) * 500;
                        return blastRadius;
                    }
                    else
                    {
                        blastRadius = 150;
                        return 150;
                    }
                }
                else if (warheadType == WarheadTypes.Kinetic)
                {
                    blastRadius = 0f;
                    return 0f;
                }
                else
                {
                    if (part.FindModuleImplementing<BDExplosivePart>() != null)
                    {
                        blastRadius = part.FindModuleImplementing<BDExplosivePart>().GetBlastRadius();
                        return blastRadius;
                    }
                    else if (part.FindModuleImplementing<MultiMissileLauncher>() != null)
                    {
                        blastRadius = BlastPhysicsUtils.CalculateBlastRange(part.FindModuleImplementing<MultiMissileLauncher>().tntMass);
                        return blastRadius;
                    }
                    else
                    {
                        blastRadius = 150;
                        return blastRadius;
                    }
                }
            }
        }

        public override void FireMissile()
        {
            if (HasFired || launched) return;
            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: Missile launch initiated! {vessel.vesselName}");

            if (SourceVessel == null)
            {
                SourceVessel = vessel;
            }
            var wpm = VesselModuleRegistry.GetMissileFire(SourceVessel, true);
            if (wpm != null) Team = wpm.Team;

            if (multiLauncher)
            {
                if (multiLauncher.isMultiLauncher)
                {
                    //multiLauncher.rippleRPM = wpm.rippleRPM;               
                    //if (wpm.rippleRPM > 0) multiLauncher.rippleRPM = wpm.rippleRPM;
                    multiLauncher.Team = Team;
                    launched = true;
                    if (reloadableRail && reloadableRail.ammoCount >= 1 || BDArmorySettings.INFINITE_ORDINANCE)
                    {
                        if (wpm)
                            wpm.UpdateQueuedLaunches(targetVessel, this, true);
                        if (radarTarget.exists && radarTarget.lockedByRadar && radarTarget.lockedByRadar.vessel != SourceVessel)
                        {
                            MissileFire datalinkwpm = VesselModuleRegistry.GetMissileFire(radarTarget.lockedByRadar.vessel, true);
                            if (datalinkwpm)
                                datalinkwpm.UpdateQueuedLaunches(targetVessel, this, true, false);
                        }
                        multiLauncher.fireMissile();
                    }
                    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: firing Multilauncher! {vessel.vesselName}; {multiLauncher.subMunitionName}");
                }
                else //isClusterMissile
                {
                    if (reloadableRail && (reloadableRail.maxAmmo > 1 && (reloadableRail.ammoCount >= 1 || BDArmorySettings.INFINITE_ORDINANCE))) //clustermissile with reload module
                    {
                        if (reloadableMissile == null)
                        {
                            if (wpm)
                                wpm.UpdateQueuedLaunches(targetVessel, this, true);
                            if (radarTarget.exists && radarTarget.lockedByRadar && radarTarget.lockedByRadar.vessel != SourceVessel)
                            {
                                MissileFire datalinkwpm = VesselModuleRegistry.GetMissileFire(radarTarget.lockedByRadar.vessel, true);
                                if (datalinkwpm)
                                    datalinkwpm.UpdateQueuedLaunches(targetVessel, this, true, false);
                            }
                            reloadableMissile = StartCoroutine(FireReloadableMissile());
                        }
                        launched = true;
                    }
                    else //standard non-reloadable missile
                    {
                        multiLauncher.missileSpawner.MissileName = multiLauncher.subMunitionName;
                        multiLauncher.missileSpawner.UpdateMissileValues();
                        DetonationDistance = multiLauncher.clusterMissileTriggerDist;
                        blastRadius = multiLauncher.clusterMissileTriggerDist;
                        multiLauncher.isLaunchedClusterMissile = true;
                        TimeFired = Time.time;
                        part.decouple(0);
                        part.Unpack();
                        TargetPosition = vessel.ReferenceTransform.position + vessel.ReferenceTransform.up * 5000; //set initial target position so if no target update, missileBase will count a miss if it nears this point or is flying post-thrust
                        MissileLaunch();
                        BDATargetManager.FiredMissiles.Add(this);
                        if (wpm != null)
                        {
                            wpm.heatTarget = TargetSignatureData.noTarget;
                            GpsUpdateMax = wpm.GpsUpdateMax;
                            wpm.UpdateMissilesAway(targetVessel, this);
                        }

                        if (radarTarget.exists && radarTarget.lockedByRadar && radarTarget.lockedByRadar.vessel != SourceVessel)
                        {
                            MissileFire datalinkwpm = VesselModuleRegistry.GetMissileFire(radarTarget.lockedByRadar.vessel, true);
                            if (datalinkwpm)
                                datalinkwpm.UpdateMissilesAway(targetVessel, this, false);
                        }

                        launched = true;
                    }
                }
            }
            else
            {
                if (reloadableRail && (reloadableRail.ammoCount >= 1 || BDArmorySettings.INFINITE_ORDINANCE))
                {
                    if (reloadableMissile == null)
                    {
                        if (wpm)
                            wpm.UpdateQueuedLaunches(targetVessel, this, true);
                        if (radarTarget.exists && radarTarget.lockedByRadar && radarTarget.lockedByRadar.vessel != SourceVessel)
                        {
                            MissileFire datalinkwpm = VesselModuleRegistry.GetMissileFire(radarTarget.lockedByRadar.vessel, true);
                            if (datalinkwpm)
                                datalinkwpm.UpdateQueuedLaunches(targetVessel, this, true, false);
                        }
                        reloadableMissile = StartCoroutine(FireReloadableMissile());
                    }
                    launched = true;
                }
                else
                {
                    TimeFired = Time.time;
                    part.decouple(0);
                    part.Unpack();
                    TargetPosition = transform.position + transform.forward * 5000; //set initial target position so if no target update, missileBase will count a miss if it nears this point or is flying post-thrust
                    MissileLaunch();
                    BDATargetManager.FiredMissiles.Add(this);
                    if (wpm != null)
                    {
                        wpm.heatTarget = TargetSignatureData.noTarget;
                        GpsUpdateMax = wpm.GpsUpdateMax;
                        wpm.UpdateMissilesAway(targetVessel, this);
                    }

                    if (radarTarget.exists && radarTarget.lockedByRadar && radarTarget.lockedByRadar.vessel != SourceVessel)
                    {
                        MissileFire datalinkwpm = VesselModuleRegistry.GetMissileFire(radarTarget.lockedByRadar.vessel, true);
                        if (datalinkwpm)
                            datalinkwpm.UpdateMissilesAway(targetVessel, this, false);
                    }

                    launched = true;
                }
            }
        }
        IEnumerator FireReloadableMissile()
        {
            part.partTransform.localScale = Vector3.zero;
            part.ShieldedFromAirstream = true;
            part.crashTolerance = 100;
            if (!reloadableRail.SpawnMissile(MissileReferenceTransform))
            {
                if (BDArmorySettings.DEBUG_MISSILES) Debug.LogWarning($"[BDArmory.MissileLauncher]: Failed to spawn a missile in {reloadableRail} on {vessel.vesselName}");
                yield break;
            }
            MissileLauncher ml = reloadableRail.SpawnedMissile.FindModuleImplementing<MissileLauncher>();
            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: Spawning missile {reloadableRail.SpawnedMissile.name}; type: {ml.homingType}/{ml.targetingType}");
            yield return new WaitUntilFixed(() => ml == null || ml.SetupComplete); // Wait until missile fully initialized.
            if (ml is null || ml.gameObject is null || !ml.gameObject.activeInHierarchy)
            {
                if (ml is not null) Destroy(ml); // The gameObject is gone, make sure the module goes too.
                Debug.LogWarning($"[BDArmory.MissileLauncher]: Error while spawning missile with {part.name}, MissileLauncher was null!");
                yield break;
            }

            ml.launched = true;
            var wpm = VesselModuleRegistry.GetMissileFire(SourceVessel, true);
            ml.SourceVessel = SourceVessel;
            ml.GuidanceMode = GuidanceMode;
            //wpm.SendTargetDataToMissile(ml);
            ml.TimeFired = Time.time;
            ml.DetonationDistance = DetonationDistance;
            ml.DetonateAtMinimumDistance = DetonateAtMinimumDistance;
            ml.dropTime = dropTime;
            ml.detonationTime = detonationTime;
            ml.engageAir = engageAir;
            ml.engageGround = engageGround;
            ml.engageMissile = engageMissile;
            ml.engageSLW = engageSLW;

            if (GuidanceMode == GuidanceModes.AGMBallistic)
            {
                ml.BallisticOverShootFactor = BallisticOverShootFactor; //are some of these null, and causing this to quit? 
                ml.BallisticAngle = BallisticAngle;
            }
            if (GuidanceMode == GuidanceModes.Cruise)
            {
                ml.CruiseAltitude = CruiseAltitude;
                ml.CruiseSpeed = CruiseSpeed;
                ml.CruisePredictionTime = CruisePredictionTime;
            }
            if (GuidanceMode == GuidanceModes.AAMLoft)
            {
                ml.LoftMaxAltitude = LoftMaxAltitude;
                ml.LoftRangeOverride = LoftRangeOverride;
                ml.LoftAltitudeAdvMax = LoftAltitudeAdvMax;
                ml.LoftMinAltitude = LoftMinAltitude;
                ml.LoftAngle = LoftAngle;
                ml.LoftTermAngle = LoftTermAngle;
                ml.LoftRangeFac = LoftRangeFac;
                ml.LoftVelComp = LoftVelComp;
                ml.LoftVertVelComp = LoftVertVelComp;
                //ml.LoftAltComp = LoftAltComp;
                ml.terminalHomingRange = terminalHomingRange;
                ml.homingModeTerminal = homingModeTerminal;
                ml.pronavGain = pronavGain;
                ml.loftState = LoftStates.Boost;
                ml.TimeToImpact = float.PositiveInfinity;
            }
            /*            if (GuidanceMode == GuidanceModes.AAMHybrid)
                            ml.pronavGain = pronavGain;*/
            if (GuidanceMode == GuidanceModes.APN || GuidanceMode == GuidanceModes.PN)
                ml.pronavGain = pronavGain;

            if (GuidanceMode == GuidanceModes.Kappa)
            {
                ml.kappaAngle = kappaAngle;
                ml.LoftAngle = LoftAngle;
                ml.loftState = LoftStates.Boost;
                ml.LoftTermAngle = LoftTermAngle;
                ml.LoftMaxAltitude = LoftMaxAltitude;
                ml.LoftRangeFac = LoftRangeFac;
                ml.LoftVertVelComp = LoftVertVelComp;
                ml.LoftRangeOverride = LoftRangeOverride;
            }

            ml.terminalHoming = terminalHoming;
            if (terminalHoming)
            {
                if (homingModeTerminal == GuidanceModes.AGMBallistic)
                {
                    ml.BallisticOverShootFactor = BallisticOverShootFactor; //are some of these null, and causeing this to quit? 
                    ml.BallisticAngle = BallisticAngle;
                }
                if (homingModeTerminal == GuidanceModes.Cruise)
                {
                    ml.CruiseAltitude = CruiseAltitude;
                    ml.CruiseSpeed = CruiseSpeed;
                    ml.CruisePredictionTime = CruisePredictionTime;
                }
                if (homingModeTerminal == GuidanceModes.AAMLoft)
                {
                    ml.LoftMaxAltitude = LoftMaxAltitude;
                    ml.LoftRangeOverride = LoftRangeOverride;
                    ml.LoftAltitudeAdvMax = LoftAltitudeAdvMax;
                    ml.LoftMinAltitude = LoftMinAltitude;
                    ml.LoftAngle = LoftAngle;
                    ml.LoftTermAngle = LoftTermAngle;
                    ml.LoftRangeFac = LoftRangeFac;
                    ml.LoftVelComp = LoftVelComp;
                    ml.LoftVertVelComp = LoftVertVelComp;
                    //ml.LoftAltComp = LoftAltComp;
                    ml.pronavGain = pronavGain;
                    ml.loftState = LoftStates.Boost;
                    ml.TimeToImpact = float.PositiveInfinity;
                }
                if (homingModeTerminal == GuidanceModes.APN || homingModeTerminal == GuidanceModes.PN)
                    ml.pronavGain = pronavGain;

                if (homingModeTerminal == GuidanceModes.Kappa)
                {
                    ml.kappaAngle = kappaAngle;
                    ml.LoftAngle = LoftAngle;
                    ml.loftState = LoftStates.Boost;
                    ml.LoftTermAngle = LoftTermAngle;
                    ml.LoftMaxAltitude = LoftMaxAltitude;
                    ml.LoftRangeFac = LoftRangeFac;
                    ml.LoftVertVelComp = LoftVertVelComp;
                    ml.LoftRangeOverride = LoftRangeOverride;
                }

                ml.terminalHomingRange = terminalHomingRange;
                ml.homingModeTerminal = homingModeTerminal;
                ml.terminalHomingActive = false;
            }

            ml.decoupleForward = decoupleForward;
            ml.decoupleSpeed = decoupleSpeed;
            if (GuidanceMode == GuidanceModes.AGM)
                ml.maxAltitude = maxAltitude;
            ml.terminalGuidanceShouldActivate = terminalGuidanceShouldActivate;
            ml.guidanceActive = true;

            BDATargetManager.FiredMissiles.Add(ml);
            if (wpm != null)
            {
                ml.Team = wpm.Team;
                wpm.SendTargetDataToMissile(ml, targetVessel != null ? targetVessel.Vessel : null, true, new MissileFire.TargetData(targetGPSCoords, TimeOfLastINS, INStimetogo), true);
                wpm.heatTarget = TargetSignatureData.noTarget;
                ml.GpsUpdateMax = wpm.GpsUpdateMax;
                wpm.UpdateQueuedLaunches(targetVessel, ml, false);
                wpm.UpdateMissilesAway(targetVessel, ml);
            }

            if (ml.radarTarget.exists && ml.radarTarget.lockedByRadar && ml.radarTarget.lockedByRadar.vessel != ml.SourceVessel)
            {
                MissileFire datalinkwpm = VesselModuleRegistry.GetMissileFire(ml.radarTarget.lockedByRadar.vessel, true);
                if (datalinkwpm)
                {
                    datalinkwpm.UpdateQueuedLaunches(targetVessel, ml, false, false);
                    datalinkwpm.UpdateMissilesAway(targetVessel, ml, false);
                }
            }

            ml.TargetPosition = transform.position + (multiLauncher ? vessel.ReferenceTransform.up * 5000 : transform.forward * 5000); //set initial target position so if no target update, missileBase will count a miss if it nears this point or is flying post-thrust
            ml.MissileLaunch();
            GetMissileCount();
            if (reloadableRail.railAmmo < 1 && reloadableRail.ammoCount > 0 || BDArmorySettings.INFINITE_ORDINANCE)
            {
                if (!(reloadRoutine != null))
                {
                    reloadRoutine = StartCoroutine(MissileReload());
                    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher] reloading standard missile");
                }
            }
            reloadableMissile = null;
        }
        public void MissileLaunch()
        {
            // if (gameObject is null || !gameObject.activeInHierarchy) { Debug.LogError($"[BDArmory.MissileLauncher]: Trying to fire non-existent missile {missileName} {(reloadableRail != null ? " (reloadable)" : "")} on {SourceVesselName} at {TargetVesselName}!"); return; }
            HasFired = true;
            try // FIXME Remove this once the fix is sufficiently tested.
            {
                GameEvents.onPartDie.Add(PartDie);

                if (GetComponentInChildren<KSPParticleEmitter>())
                {
                    BDArmorySetup.numberOfParticleEmitters++;
                }

                if (sfAudioSource == null) SetupAudio();
                sfAudioSource.PlayOneShot(SoundUtils.GetAudioClip("BDArmory/Sounds/deployClick"));
                //SourceVessel = vessel;

                //TARGETING
                startDirection = transform.forward;

                if (maxAltitude == 0) // && GuidanceMode != GuidanceModes.Lofted)
                {
                    if (targetVessel != null) maxAltitude = (float)Math.Max(vessel.radarAltitude, targetVessel.Vessel.radarAltitude) + 1000;
                    else maxAltitude = (float)vessel.radarAltitude + 2500;
                }
                SetLaserTargeting();
                SetAntiRadTargeting();

                part.force_activate();
                part.gTolerance = 999;
                vessel.situation = Vessel.Situations.FLYING;
                part.rb.isKinematic = false;
                part.bodyLiftMultiplier = 0;
                part.dragModel = Part.DragModel.NONE;

                //add target info to vessel
                AddTargetInfoToVessel();
                StartCoroutine(DecoupleRoutine());
                if (BDArmorySettings.DEBUG_MISSILES) shortName = $"{SourceVessel.GetName()}'s {GetShortName()}";
                vessel.vesselName = GetShortName();
                vessel.vesselType = VesselType.Probe;
                //setting ref transform for navball
                GameObject refObject = new GameObject();
                refObject.transform.rotation = Quaternion.LookRotation(-transform.up, transform.forward);
                refObject.transform.parent = transform;
                part.SetReferenceTransform(refObject.transform);
                vessel.SetReferenceTransform(part);
                vesselReferenceTransform = refObject.transform;
                DetonationDistanceState = DetonationDistanceStates.NotSafe;
                MissileState = MissileStates.Drop;
                part.crashTolerance = torpedo ? waterImpactTolerance : 9999; //to combat stresses of launch, missiles generate a lot of G Force
                part.explosionPotential = 0; // Minimise the default part explosion FX that sometimes gets offset from the main explosion.
                vacuumClearanceState = (GuidanceMode == GuidanceModes.Orbital && vacuumSteerable && part.atmDensity <= 0.001f && missileTurret == null) ? // vessel.InVacuum() not updated, will return 0, so use part.atmDensity check
                    VacuumClearanceStates.Clearing : VacuumClearanceStates.Cleared; // Set up clearance check if missile is vacuumSteerable, and is in space, and was not launched from a turret

                CruiseSpeed = Mathf.Min(CruiseSpeed, maxCruiseSpeed);

                StartCoroutine(MissileRoutine());
                List<BDWarheadBase> tntList = part.FindModulesImplementing<BDWarheadBase>();
                foreach (BDWarheadBase tnt in tntList)
                {
                    tnt.Team = Team;
                    tnt.sourcevessel = SourceVessel;
                }
                if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher]: Missile Launched!");
                if (BDArmorySettings.CAMERA_SWITCH_INCLUDE_MISSILES && SourceVessel.isActiveVessel) LoadedVesselSwitcher.Instance.ForceSwitchVessel(vessel);
            }
            catch (Exception e)
            {
                Debug.LogError("[BDArmory.MissileLauncher]: DEBUG " + e.Message + "\n" + e.StackTrace);
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null part?: " + (part == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG part: " + e2.Message); }
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null part.rb?: " + (part.rb == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG part.rb: " + e2.Message); }
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null BDATargetManager.FiredMissiles?: " + (BDATargetManager.FiredMissiles == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG BDATargetManager.FiredMissiles: " + e2.Message); }
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null vessel?: " + (vessel == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG vessel: " + e2.Message); }
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null targetVessel?: " + (targetVessel == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG targetVessel: " + e2.Message); }
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null sfAudioSource?: " + (sfAudioSource == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG sfAudioSource: " + e2.Message); }
                throw; // Re-throw the exception so behaviour is unchanged so we see it.
            }
        }

        public IEnumerator MissileReload()
        {
            reloadableRail.loadOrdnance(multiLauncher ? multiLauncher.launchTubes : 1);
            if (reloadableRail.railAmmo > 0 || BDArmorySettings.INFINITE_ORDINANCE)
            {
                if (vessel.isActiveVessel) gauge.UpdateReloadMeter(reloadTimer);
                yield return new WaitForSecondsFixed(reloadableRail.reloadTime);
                launched = false;
                part.partTransform.localScale = origScale;
                reloadTimer = 0;
                gauge.UpdateReloadMeter(1);
                if (!multiLauncher) part.crashTolerance = 5;
                if (!inCargoBay) part.ShieldedFromAirstream = false;
                if (deployableRail) deployableRail.UpdateChildrenPos();
                if (rotaryRail) rotaryRail.UpdateMissilePositions();
                if (multiLauncher) multiLauncher.PopulateMissileDummies();
                if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher] reload complete on {part.name}");
            }
            reloadRoutine = null;
        }

        IEnumerator DecoupleRoutine()
        {
            yield return new WaitForFixedUpdate();

            if (rndAngVel > 0)
            {
                part.rb.angularVelocity += UnityEngine.Random.insideUnitSphere.normalized * rndAngVel;
            }

            if (decoupleForward)
            {
                part.rb.velocity += decoupleSpeed * part.transform.forward;
                if (multiLauncher && multiLauncher.isMultiLauncher && multiLauncher.salvoSize > 1) //add some scatter to missile salvoes
                {
                    part.rb.velocity += (UnityEngine.Random.Range(-1f, 1f) * (decoupleSpeed / 4)) * part.transform.up;
                    part.rb.velocity += (UnityEngine.Random.Range(-1f, 1f) * (decoupleSpeed / 4)) * part.transform.right;
                }
            }
            else
            {
                part.rb.velocity += decoupleSpeed * -part.transform.up;
            }
        }

        /// <summary>
        /// Fires the missileBase on target vessel.  Used by AI currently.
        /// </summary>
        /// <param name="v">V.</param>
        public void FireMissileOnTarget(Vessel v)
        {
            if (!HasFired)
            {
                targetVessel = v.gameObject.GetComponent<TargetInfo>();
                FireMissile();
            }
        }

        void OnDisable()
        {
            if (TargetingMode == TargetingModes.AntiRad)
            {
                RadarWarningReceiver.OnRadarPing -= ReceiveRadarPing;
            }
        }

        void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (!vessel.isActiveVessel) return;
            if (reloadableRail)
            {
                if (launched && reloadRoutine != null)
                {
                    reloadTimer += TimeWarp.deltaTime;
                    gauge.UpdateReloadMeter(Mathf.Clamp01(reloadTimer / reloadableRail.reloadTime));
                }
            }
            if (multiLauncher && heatTimer > 0)
            {
                heatTimer -= TimeWarp.deltaTime;
                gauge.UpdateHeatMeter(Mathf.Clamp01(heatTimer / multiLauncher.launcherCooldown));
            }
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            if (!HighLogic.LoadedSceneIsFlight) return;

            FloatingOriginCorrection();

            try // FIXME Remove this once the fix is sufficiently tested.
            {
                debugString.Length = 0;

                if (HasFired && !HasExploded && part != null)
                {
                    part.rb.isKinematic = false;
                    AntiSpin();
                    //simpleDrag
                    if (useSimpleDrag || useSimpleDragTemp)
                    {
                        SimpleDrag();
                    }

                    //flybyaudio
                    float mCamDistanceSqr = (FlightCamera.fetch.mainCamera.transform.position - vessel.CoM).sqrMagnitude;
                    float mCamRelVSqr = (float)(FlightGlobals.ActiveVessel.Velocity() - vessel.Velocity()).sqrMagnitude;
                    if (!hasPlayedFlyby
                       && FlightGlobals.ActiveVessel != vessel
                       && FlightGlobals.ActiveVessel != SourceVessel
                       && mCamDistanceSqr < 400 * 400 && mCamRelVSqr > 300 * 300
                       && mCamRelVSqr < 800 * 800
                       && Vector3.Angle(vessel.Velocity(), FlightGlobals.ActiveVessel.CoM - vessel.CoM) < 60)
                    {
                        if (sfAudioSource == null) SetupAudio();
                        sfAudioSource.PlayOneShot(SoundUtils.GetAudioClip("BDArmory/Sounds/missileFlyby"));
                        hasPlayedFlyby = true;
                    }
                    if (vessel.isActiveVessel)
                    {
                        audioSource.dopplerLevel = 0;
                    }
                    else
                    {
                        audioSource.dopplerLevel = 1f;
                    }

                    UpdateThrustForces();
                    UpdateGuidance();
                    CheckDetonationState(); // this needs to be after UpdateGuidance()
                    CheckDetonationDistance();
                    CheckCountermeasureDistance();

                    //RaycastCollisions();

                    //Timed detonation
                    if (isTimed && TimeIndex > detonationTime)
                    {
                        if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher] missile timed out; self-destructing!");
                        Detonate();
                    }
                    //debugString.AppendLine($"crashTol: {part.crashTolerance}; collider: {part.collider.enabled}; usingSimpleDrag: {(useSimpleDrag && useSimpleDragTemp)}; drag: {part.angularDrag.ToString("0.00")}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[BDArmory.MissileLauncher]: DEBUG " + e.Message + "\n" + e.StackTrace);
                // throw; // Re-throw the exception so behaviour is unchanged so we see it.
                /* FIXME this is being caused by attempting to get the wm.Team in RadarUpdateMissileLock. A similar exception occurred in BDATeamIcons, line 239
                    [ERR 12:05:24.391] Module MissileLauncher threw during OnFixedUpdate: System.NullReferenceException: Object reference not set to an instance of an object
                        at BDArmory.Radar.RadarUtils.RadarUpdateMissileLock (UnityEngine.Ray ray, System.Single fov, BDArmory.Targeting.TargetSignatureData[]& dataArray, System.Single dataPersistTime, BDArmory.Weapons.Missiles.MissileBase missile) [0x00076] in /storage/github/BDArmory/BDArmory/Radar/RadarUtils.cs:972 
                        at BDArmory.Weapons.Missiles.MissileBase.UpdateRadarTarget () [0x003d9] in /storage/github/BDArmory/BDArmory/Weapons/Missiles/MissileBase.cs:747 
                        at BDArmory.Weapons.Missiles.MissileLauncher.UpdateGuidance () [0x000ba] in /storage/github/BDArmory/BDArmory/Weapons/Missiles/MissileLauncher.cs:1134 
                        at BDArmory.Weapons.Missiles.MissileLauncher.OnFixedUpdate () [0x00593] in /storage/github/BDArmory/BDArmory/Weapons/Missiles/MissileLauncher.cs:1046 
                        at Part.ModulesOnFixedUpdate () [0x000bd] in <4deecb19beb547f19b1ff89b4c59bd84>:0 
                        UnityEngine.DebugLogHandler:LogFormat(LogType, Object, String, Object[])
                        ModuleManager.UnityLogHandle.InterceptLogHandler:LogFormat(LogType, Object, String, Object[])
                        UnityEngine.Debug:LogError(Object)
                        Part:ModulesOnFixedUpdate()
                        Part:FixedUpdate()
                */
            }
            if (reloadableRail)
            {
                if (OldInfAmmo != BDArmorySettings.INFINITE_ORDINANCE)
                {
                    if (reloadableRail.railAmmo < 1 && BDArmorySettings.INFINITE_ORDINANCE)
                    {
                        if (!(reloadRoutine != null))
                        {
                            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher] Infinite Ammo enabled, reloading");
                            reloadRoutine = StartCoroutine(MissileReload());
                        }
                    }
                    OldInfAmmo = BDArmorySettings.INFINITE_ORDINANCE;
                }
            }
        }

        protected override void InitializeCountermeasures()
        {
            var ECM = part.FindModuleImplementing<ModuleECMJammer>();
            if (ECM != null)
            {
                ECM.EnableJammer();
                CMenabled = true;
            }

            missileCM = part.FindModulesImplementing<CMDropper>();
            missileCM.Sort((a, b) => b.priority.CompareTo(a.priority)); // Sort from highest to lowest priority
            missileCMTime = Time.time;
            int currPriority = 0;
            foreach (CMDropper dropper in missileCM)
            {
                if (dropper.cmType == CMDropper.CountermeasureTypes.Chaff)
                    dropper.UpdateVCI();
                dropper.SetupAudio();
                if (currPriority <= dropper.Priority)
                {
                    if (dropper.DropCM())
                    {
                        currPriority = dropper.Priority;
                    }
                }
                CMenabled = true;
            }
        }

        protected override void DropCountermeasures()
        {
            int currPriority = 0;
            foreach (CMDropper dropper in missileCM)
            {
                if (currPriority <= dropper.Priority)
                {
                    if (dropper.DropCM())
                        currPriority = dropper.Priority;
                }
            }
        }

        private void CheckMiss()
        {
            if (weaponClass == WeaponClasses.Bomb) return;
            float sqrDist = (float)((TargetPosition + (TargetVelocity * Time.fixedDeltaTime)) - (vessel.CoM + (vessel.Velocity() * Time.fixedDeltaTime))).sqrMagnitude;
            bool targetBehindMissile = !TargetAcquired || (!(MissileState != MissileStates.PostThrust && hasRCS) && Vector3.Dot(TargetPosition - vessel.CoM, transform.forward) < 0f); // Target is not acquired or we are behind it and not an RCS missile
            if (sqrDist < 160000 || MissileState == MissileStates.PostThrust || (targetBehindMissile && sqrDist > 1000000)) //missile has come within 400m, is post thrust, or > 1km behind target
            {
                checkMiss = true;
            }
            if (maxAltitude != 0f)
            {
                if (vessel.altitude >= maxAltitude) checkMiss = true;
            }

            //kill guidance if missileBase has missed
            if (!HasMissed && checkMiss)
            {
                Vector3 tgtVel = TargetVelocity == Vector3.zero && targetVessel != null ? targetVessel.Vessel.Velocity() : TargetVelocity;
                bool noProgress = MissileState == MissileStates.PostThrust && ((Vector3.Dot(vessel.Velocity() - tgtVel, TargetPosition - vessel.CoM) < 0) ||
                    (!vessel.InVacuum() && vessel.srfSpeed < GetKinematicSpeed() && weaponClass == WeaponClasses.Missile));
                bool pastGracePeriod = TimeIndex > ((MissileState == MissileStates.PostThrust ? 1 : optimumAirspeed / vessel.speed) * ((vessel.LandedOrSplashed ? 0f : dropTime) + guidanceDelay + Mathf.Clamp(maxTurnRateDPS / 15f, 1, 8))); //180f / maxTurnRateDPS);
                if ((pastGracePeriod && targetBehindMissile) || noProgress) // Check that we're not moving away from the target after a grace period
                {
                    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: Missile has missed({(noProgress ? "no progress" : !TargetAcquired ? "no target" : "past target")})!");

                    if (vessel.altitude >= maxAltitude && maxAltitude != 0f)
                        if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher]: CheckMiss trigged by MaxAltitude");

                    HasMissed = true;
                    guidanceActive = false;

                    MissileLauncher launcher = this as MissileLauncher;
                    if (launcher != null)
                    {
                        if (launcher.hasRCS) launcher.KillRCS();
                    }

                    var distThreshold = 0.5f * GetBlastRadius();
                    if (sqrDist < distThreshold * distThreshold) part.Destroy();
                    if (FuseFailed) part.Destroy();

                    isTimed = true;
                    detonationTime = TimeIndex + 1.5f;
                    if (BDArmorySettings.CAMERA_SWITCH_INCLUDE_MISSILES && vessel.isActiveVessel) LoadedVesselSwitcher.Instance.TriggerSwitchVessel();
                    return;
                }
            }
        }

        string debugGuidanceTarget;
        void UpdateGuidance()
        {
            if (guidanceActive && guidanceFailureRatePerFrame > 0f)
                if (UnityEngine.Random.Range(0f, 1f) < guidanceFailureRatePerFrame)
                {
                    guidanceActive = false;
                    BDATargetManager.FiredMissiles.Remove(this);
                    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher]: Missile Guidance Failed!");
                }

            if (guidanceActive)
            {
                if (hasGimbal)
                {
                    hasGimbal = false;
                    maxOffBoresight = maxSeekerGimbal;
                }
                switch (TargetingMode)
                {
                    case TargetingModes.Heat:
                        UpdateHeatTarget();
                        if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES)
                        {
                            if (heatTarget.vessel)
                                debugGuidanceTarget = $"{heatTarget.vessel.GetName()} {heatTarget.signalStrength}";
                            else if (heatTarget.signalStrength > 0)
                                debugGuidanceTarget = $"Flare {heatTarget.signalStrength}";
                        }
                        break;
                    case TargetingModes.Radar:
                        UpdateRadarTarget();
                        if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES)
                        {
                            if (radarTarget.vessel)
                                debugGuidanceTarget = $"{radarTarget.vessel.GetName()} {radarTarget.signalStrength}";
                            else if (radarTarget.signalStrength > 0)
                                debugGuidanceTarget = $"Chaff {radarTarget.signalStrength}";
                        }
                        break;
                    case TargetingModes.Laser:
                        UpdateLaserTarget();
                        if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES)
                        {
                            debugGuidanceTarget = TargetPosition.ToString();
                        }
                        break;
                    case TargetingModes.Gps:
                        UpdateGPSTarget();
                        if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES)
                        {
                            debugGuidanceTarget = UpdateGPSTarget().ToString();
                        }
                        break;
                    case TargetingModes.AntiRad:
                        UpdateAntiRadiationTarget();
                        if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES)
                        {
                            debugGuidanceTarget = TargetPosition.ToString();
                        }
                        break;
                    case TargetingModes.Inertial:
                        UpdateInertialTarget();
                        if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES)
                        {
                            debugGuidanceTarget = $"TgtPos: {TargetPosition}; Drift: {(TargetPosition - VectorUtils.GetWorldSurfacePostion(targetGPSCoords, vessel.mainBody)).ToString()}";
                        }
                        break;
                    default:
                        if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES)
                        {
                            TargetPosition = transform.position + (startDirection * 500);
                            debugGuidanceTarget = TargetPosition.ToString();
                        }
                        break;
                }

                UpdateTerminalGuidance();
            }

            if (MissileState != MissileStates.Idle && MissileState != MissileStates.Drop) //guidance
            {
                //guidance and attitude stabilisation scales to atmospheric density. //use part.atmDensity
                float atmosMultiplier = Mathf.Clamp01(2.5f * (float)FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(vessel.CoM), FlightGlobals.getExternalTemperature(vessel.CoM), FlightGlobals.currentMainBody));

                if (vessel.srfSpeed < optimumAirspeed)
                {
                    float optimumSpeedFactor = (float)vessel.srfSpeed / (2 * optimumAirspeed);
                    controlAuthority = Mathf.Clamp01(atmosMultiplier * (-Mathf.Abs(2 * optimumSpeedFactor - 1) + 1));
                }
                else
                {
                    controlAuthority = Mathf.Clamp01(atmosMultiplier);
                }

                if (vacuumSteerable)
                {
                    controlAuthority = 1;
                }

                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES) debugString.AppendLine($"controlAuthority: {controlAuthority}");

                if (guidanceActive)
                {
                    WarnTarget();
                    if (TimeIndex - dropTime > guidanceDelay)
                    {
                        //if (targetVessel && targetVessel.loaded)
                        //{
                        //   Vector3 targetCoMPos = targetVessel.CoM;
                        //    TargetPosition = targetCoMPos + targetVessel.Velocity() * Time.fixedDeltaTime;
                        //}

                        // Increase turn rate gradually after launch, unless vacuum steerable in space
                        float turnRateDPS = maxTurnRateDPS;
                        if (!((vacuumSteerable && vessel.InVacuum()) || boostTime == 0f))
                            turnRateDPS = Mathf.Clamp(((TimeIndex - dropTime) / boostTime) * maxTurnRateDPS * 25f, 0, maxTurnRateDPS);
                        if (!hasRCS)
                        {
                            turnRateDPS *= controlAuthority;
                        }

                        //decrease turn rate after thrust cuts out
                        if (TimeIndex > dropTime + boostTime + cruiseDelay + cruiseTime)
                        {
                            var clampedTurnRate = Mathf.Clamp(maxTurnRateDPS - ((TimeIndex - dropTime - boostTime - cruiseDelay - cruiseTime) * 0.45f),
                                1, maxTurnRateDPS);
                            turnRateDPS = clampedTurnRate;

                            if (!vacuumSteerable)
                            {
                                turnRateDPS *= atmosMultiplier;
                            }

                            if (hasRCS)
                            {
                                turnRateDPS = 0;
                            }
                        }

                        if (hasRCS)
                        {
                            if (turnRateDPS > 0)
                            {
                                DoRCS();
                            }
                            else
                            {
                                KillRCS();
                            }
                        }
                        debugTurnRate = turnRateDPS;

                        finalMaxTorque = Mathf.Clamp((TimeIndex - dropTime) * torqueRampUp, 0, currMaxTorque); //ramp up torque

                        if (terminalHoming && !terminalHomingActive)
                        {
                            if (Vector3.SqrMagnitude(TargetPosition - vessel.CoM) < terminalHomingRange * terminalHomingRange)
                            {
                                GuidanceMode = homingModeTerminal;
                                terminalHomingActive = true;
                                Throttle = 1f;
                                if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: Terminal with {GuidanceMode}");
                            }
                        }
                        switch (GuidanceMode)
                        {
                            case GuidanceModes.AAMLead:
                            case GuidanceModes.APN:
                            case GuidanceModes.PN:
                            case GuidanceModes.AAMLoft:
                            case GuidanceModes.AAMPure:
                            case GuidanceModes.Kappa:
                                //GuidanceModes.AAMHybrid:
                                AAMGuidance();
                                break;
                            case GuidanceModes.AGM:
                                AGMGuidance();
                                break;
                            case GuidanceModes.AGMBallistic:
                                AGMBallisticGuidance();
                                break;
                            case GuidanceModes.BeamRiding:
                                BeamRideGuidance();
                                break;
                            case GuidanceModes.Orbital: //nee GuidanceModes.RCS
                                OrbitalGuidance(turnRateDPS);
                                break;
                            case GuidanceModes.Cruise:
                                CruiseGuidance();
                                break;
                            case GuidanceModes.Weave:
                                AAMGuidance();
                                break;
                            case GuidanceModes.SLW:
                                SLWGuidance();
                                break;
                            case GuidanceModes.None:
                                DoAero(TargetPosition);
                                CheckMiss();
                                break;
                        }
                    }
                    else
                        DoAero(TargetPosition);
                }
                else
                {
                    CheckMiss();
                    if (aero)
                    {
                        aeroTorque = MissileGuidance.DoAeroForces(this, TargetPosition, currLiftArea, currDragArea, .25f, aeroTorque, currMaxTorque, currMaxTorqueAero, 0.1f, MissileGuidance.DefaultLiftCurve, MissileGuidance.DefaultDragCurve);
                    }
                }

                if (aero && aeroSteerDamping > 0f)
                {
                    part.rb.AddRelativeTorque(-aeroSteerDamping * part.transform.InverseTransformDirection(part.rb.angularVelocity));
                }

                if (hasRCS && !guidanceActive)
                {
                    KillRCS();
                }
            }

            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES)
            {
                if (guidanceActive) debugString.AppendLine("Missile target=" + debugGuidanceTarget);
                else debugString.AppendLine("Guidance inactive");

                debugString.AppendLine("Source vessel=" + (SourceVessel != null ? SourceVessel.GetName() : "null"));

                debugString.AppendLine("Target vessel=" + ((targetVessel != null && targetVessel.Vessel != null) ? targetVessel.Vessel.GetName() : "null"));

                if (!(BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES)) return;
                var distance = (TargetPosition - vessel.CoM).magnitude;
                debugString.AppendLine($"Target distance: {(distance > 1000 ? $" {distance / 1000:F1} km" : $" {distance:F0} m")}, closing speed: {Vector3.Dot(vessel.Velocity() - TargetVelocity, GetForwardTransform()):F1} m/s");
            }
        }

        // feature_engagementenvelope: terminal guidance mode for cruise missiles
        private void UpdateTerminalGuidance()
        {
            Vector3 tempTargetPos = TargetPosition;

            bool scanOverride = false;

            if (TargetingMode == TargetingModes.Inertial && TimeOfLastINS > 0)
            {
                float deltaT = TimeIndex - TimeOfLastINS;
                tempTargetPos = VectorUtils.GetWorldSurfacePostion(TargetINSCoords, vessel.mainBody);
                if (deltaT > GpsUpdateMax)
                {
                    deltaT /= INStimetogo;

                    tempTargetPos = new Vector3((1f - deltaT) * tempTargetPos.x + deltaT * TargetPosition.x, (1f - deltaT) * tempTargetPos.y + deltaT * TargetPosition.y, (1f - deltaT) * tempTargetPos.z + deltaT * TargetPosition.z);
                }
            }

            if (!TargetAcquired && targetVessel == null)
                scanOverride = true; // Allow missiles to go to their terminal guidance when dumbfired

            // check if guidance mode should be changed for terminal phase
            float distanceSqr = (tempTargetPos - vessel.CoM).sqrMagnitude;

            if (terminalGuidanceShouldActivate && !terminalGuidanceActive && (TargetingModeTerminal != TargetingModes.None) && (scanOverride || (distanceSqr < terminalGuidanceDistance * terminalGuidanceDistance)))
            {
                if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher][Terminal Guidance]: missile {GetPartName()} updating targeting mode: {terminalGuidanceType}");

                TargetAcquired = false;

                switch (TargetingModeTerminal)
                {
                    case TargetingModes.Heat:
                        // gets ground heat targets and after locking one, disallows the lock to break to another target
                        
                        if (activeRadarRange < 0 && torpedo)
                            heatTarget = BDATargetManager.GetAcousticTarget(SourceVessel, vessel, new Ray(vessel.CoM, tempTargetPos - vessel.CoM), TargetSignatureData.noTarget, lockedSensorFOV / 2, heatThreshold, targetCoM, lockedSensorFOVBias, lockedSensorVelocityBias, lockedSensorVelocityMagnitudeBias, lockedSensorMinAngularVelocity,
                                (SourceVessel == null ? null : SourceVessel.gameObject == null ? null : SourceVessel.gameObject.GetComponent<MissileFire>()), targetVessel, IFF: hasIFF);
                        else
                            heatTarget = BDATargetManager.GetHeatTarget(SourceVessel, vessel, new Ray(vessel.CoM, tempTargetPos - vessel.CoM), TargetSignatureData.noTarget, lockedSensorFOV / 2, heatThreshold, frontAspectHeatModifier, uncagedLock, targetCoM, lockedSensorFOVBias, lockedSensorVelocityBias, lockedSensorVelocityMagnitudeBias, lockedSensorMinAngularVelocity, SourceVessel ? VesselModuleRegistry.GetModule<MissileFire>(SourceVessel) : null, targetVessel, IFF: hasIFF);
                        if (heatTarget.exists && CheckTargetEngagementEnvelope(heatTarget.targetInfo))
                        {
                            if (BDArmorySettings.DEBUG_MISSILES)
                            {
                                Debug.Log($"[BDArmory.MissileLauncher][Terminal Guidance]: {(activeRadarRange < 0 && torpedo ? "Acoustic" : "Heat")} target acquired! Position: {heatTarget.position}, {(activeRadarRange < 0 && torpedo ? "Noise" : "Heat")}score: {heatTarget.signalStrength}");
                            }
                            TargetAcquired = true;
                            TargetPosition = heatTarget.position;
                            TargetVelocity = heatTarget.velocity;
                            TargetAcceleration = heatTarget.acceleration;
                            //targetVessel = heatTarget.targetInfo; will mess with AI MissilesAway and potentially result in ripplefired IR missiles against an enemy actively flaring and decoying heaters.
                            lockFailTimer = -1; // ensures proper entry into UpdateHeatTarget()

                            // Disable terminal guidance and switch to regular heat guidance for next update
                            terminalGuidanceShouldActivate = false;
                            TargetingMode = TargetingModes.Heat;
                            terminalGuidanceActive = true;

                            // Adjust heat score based on distance missile will travel in the next update
                            if (!torpedo && heatTarget.signalStrength > 0)
                            {
                                float currentFactor = (1400 * 1400) / Mathf.Clamp((heatTarget.position - vessel.CoM).sqrMagnitude, 90000, 36000000);
                                Vector3 currVel = vessel.Velocity();
                                heatTarget.position = heatTarget.position + heatTarget.velocity * Time.fixedDeltaTime;
                                heatTarget.velocity = heatTarget.velocity + heatTarget.acceleration * Time.fixedDeltaTime;
                                float futureFactor = (1400 * 1400) / Mathf.Clamp((heatTarget.position - (vessel.CoM + (currVel * Time.fixedDeltaTime))).sqrMagnitude, 90000, 36000000);
                                heatTarget.signalStrength *= futureFactor / currentFactor;
                            }
                        }
                        else
                        {
                            if (!dumbTerminalGuidance)
                            {
                                TargetAcquired = true;
                                TargetVelocity = Vector3.zero;
                                TargetAcceleration = Vector3.zero;
                                //continue towards primary guidance targetPosition until heat lock acquired
                            }
                            if (BDArmorySettings.DEBUG_MISSILES)
                            {
                                Debug.Log("[BDArmory.MissileLauncher][Terminal Guidance]: Missile heatseeker could not acquire a target lock, reverting to default guidance.");
                            }
                        }
                        break;

                    case TargetingModes.Radar:

                        // pretend we have an active radar seeker for ground targets:
                        //TargetSignatureData[] scannedTargets = new TargetSignatureData[5];
                        if (scannedTargets == null) scannedTargets = new TargetSignatureData[BDATargetManager.LoadedVessels.Count];
                        TargetSignatureData.ResetTSDArray(ref scannedTargets);
                        Ray ray = new Ray(vessel.CoM, GetForwardTransform());

                        //RadarUtils.UpdateRadarLock(ray, maxOffBoresight, activeRadarMinThresh, ref scannedTargets, 0.4f, true, RadarWarningReceiver.RWRThreatTypes.MissileLock, true);
                        RadarUtils.RadarUpdateMissileLock(ray, maxOffBoresight, ref scannedTargets, 0.4f, this);
                        float sqrThresh = terminalGuidanceDistance * terminalGuidanceDistance * 2.25f; // (terminalGuidanceDistance * 1.5f)^2

                        //float smallestAngle = maxOffBoresight;
                        //TargetSignatureData lockedTarget = TargetSignatureData.noTarget;

                        float currDist = float.PositiveInfinity;
                        float prevDist = float.PositiveInfinity;
                        int lockIndex = -1;

                        if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher][Terminal Guidance]: Active radar found: {scannedTargets.Length} targets.");

                        for (int i = 0; i < scannedTargets.Length; i++)
                        {
                            if (scannedTargets[i].exists && (!hasIFF || !Team.IsFriendly(scannedTargets[i].Team)))
                            {
                                currDist = (scannedTargets[i].predictedPosition - tempTargetPos).sqrMagnitude;

                                if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher][Terminal Guidance]: Target: {scannedTargets[i].vessel.name} has currDist: {currDist}.");

                                //re-check engagement envelope, only lock appropriate targets
                                if (currDist < sqrThresh && currDist < prevDist && CheckTargetEngagementEnvelope(scannedTargets[i].targetInfo))
                                {
                                    prevDist = currDist;

                                    lockIndex = i;
                                    ActiveRadar = true;
                                }
                            }
                            //if (!scannedTargets[i].exists)
                            //    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher][Terminal Guidance]: Target: {i} doesn't exist!.");
                            //if (scannedTargets[i].exists && Team.IsFriendly(scannedTargets[i].Team))
                            //    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher][Terminal Guidance]: Target: {scannedTargets[i].vessel.name} is friendly, continuing.");

                        }

                        if (lockIndex >= 0)
                        {
                            radarTarget = scannedTargets[lockIndex];
                            TargetAcquired = true;
                            TargetPosition = radarTarget.predictedPositionWithChaffFactor(chaffEffectivity);
                            TargetVelocity = radarTarget.velocity;
                            TargetAcceleration = radarTarget.acceleration;
                            targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);

                            if (weaponClass == WeaponClasses.SLW)
                                RadarWarningReceiver.PingRWR(new Ray(vessel.CoM, radarTarget.predictedPosition - vessel.CoM), 45, RadarWarningReceiver.RWRThreatTypes.Torpedo, 2f);
                            else
                                RadarWarningReceiver.PingRWR(new Ray(vessel.CoM, radarTarget.predictedPosition - vessel.CoM), 45, RadarWarningReceiver.RWRThreatTypes.MissileLaunch, 2f);

                            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher][Terminal Guidance]: Pitbull! Radar missileBase has gone active.  Radar sig strength: {radarTarget.signalStrength:0.0} - target: {radarTarget.vessel.name}");
                            terminalGuidanceActive = true;
                        }
                        else
                        {
                            TargetAcquired = true;
                            TargetPosition = VectorUtils.GetWorldSurfacePostion(UpdateGPSTarget(), vessel.mainBody); //putting back the GPS target if no radar target found
                            TargetVelocity = Vector3.zero;
                            TargetAcceleration = Vector3.zero;
                            targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody); //tgtPos/tgtGPS should really be not set here, so the last valid postion/coords are used, in case of non-GPS primary guidance
                            if (dumbTerminalGuidance)
                                terminalGuidanceActive = true;
                            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher][Terminal Guidance]: Missile radar could not acquire a target lock - Defaulting to GPS Target");
                        }
                        break;

                    case TargetingModes.Laser:
                        // not very useful, currently unsupported!
                        break;

                    case TargetingModes.Gps:
                        // from gps to gps -> no actions need to be done!
                        break;
                    case TargetingModes.Inertial:
                        // Not sure *why* you'd use this for TerminalGuideance, but ok...
                        TargetAcquired = true;
                        if (targetVessel != null) TargetPosition = VectorUtils.GetWorldSurfacePostion(MissileGuidance.GetAirToAirFireSolution(this, targetVessel.Vessel.CoM, TargetVelocity), vessel.mainBody);
                        TargetVelocity = Vector3.zero;
                        TargetAcceleration = Vector3.zero;
                        terminalGuidanceActive = true;
                        break;

                    case TargetingModes.AntiRad:
                        TargetAcquired = true;
                        targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody); // Set the GPS coordinates from the current target position.
                        SetAntiRadTargeting(); //should then already work automatically via OnReceiveRadarPing
                        if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher][Terminal Guidance]: Antiradiation mode set! Waiting for radar signals...");
                        terminalGuidanceActive = true;
                        break;
                }
                if (dumbTerminalGuidance || terminalGuidanceActive)
                {
                    TargetingMode = TargetingModeTerminal;
                    terminalGuidanceActive = true;
                    terminalGuidanceShouldActivate = false;
                }
            }
        }

        void UpdateThrustForces()
        {
            if (MissileState == MissileStates.PostThrust) return;
            if (weaponClass == WeaponClasses.SLW && FlightGlobals.getAltitudeAtPos(vessel.CoM) > 0) return; //#710, no torp thrust out of water
            if (currentThrust * Throttle > 0)
            {
                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES)
                {
                    debugString.AppendLine($"Missile thrust= {currentThrust * Throttle:F3} kN");
                    debugString.AppendLine($"Missile mass= {part.mass * 1000f:F1} kg");
                }
                part.rb.AddRelativeForce(currentThrust * Throttle * Vector3.forward);
            }
        }

        IEnumerator MissileRoutine()
        {
            MissileState = MissileStates.Drop;
            if (engineFailureRate > 0f)
                if (UnityEngine.Random.Range(0f, 1f) < engineFailureRate)
                {
                    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher]: Missile Engine Failed on Launch!");
                    yield return new WaitForSecondsFixed(2f); // Pilot reaction time
                    BDATargetManager.FiredMissiles.Remove(this);
                    yield break;
                }

            if (deployStates != null) StartCoroutine(DeployAnimRoutine());
            yield return new WaitForSecondsFixed(dropTime);
            if (animStates != null) StartCoroutine(FlightAnimRoutine());
            yield return StartCoroutine(BoostRoutine());

            yield return new WaitForSecondsFixed(cruiseDelay);
            if (cruiseRangeTrigger > 0)
                yield return new WaitUntilFixed(checkCruiseRangeTrigger);

            if (cruiseStates != null) StartCoroutine(CruiseAnimRoutine());
            yield return StartCoroutine(CruiseRoutine());
        }

        bool checkCruiseRangeTrigger()
        {
            float sqrRange = (TargetPosition - part.rb.position).sqrMagnitude;

            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: Check cruise range trigger range: {BDAMath.Sqrt(sqrRange)}");

            if (sqrRange < cruiseRangeTrigger * cruiseRangeTrigger)
            {
                if (cruiseTerminationFrames < 5)
                {
                    cruiseTerminationFrames++;
                    return false;
                }

                cruiseTerminationFrames = 0;
                return true;
            }

            cruiseTerminationFrames = 0;
            return false;
        }

        IEnumerator DeployAnimRoutine()
        {
            yield return new WaitForSecondsFixed(deployTime);
            if (deployStates == null)
            {
                if (BDArmorySettings.DEBUG_MISSILES) Debug.LogWarning("[BDArmory.MissileLauncher]: deployStates was null, aborting AnimRoutine.");
                yield break;
            }

            if (!string.IsNullOrEmpty(deployAnimationName))
            {
                deployed = true;

                applyDeployedLiftDrag();
                MissileGuidance.setupTorqueAoALimit(this, currLiftArea, currDragArea);

                using (var anim = deployStates.AsEnumerable().GetEnumerator())
                    while (anim.MoveNext())
                    {
                        if (anim.Current == null) continue;
                        anim.Current.enabled = true;
                        anim.Current.speed = 1;
                    }
            }
        }

        private void applyDeployedLiftDrag(bool cruise = false)
        {
            int index = cruise ? 3 : 2;
            // Apply the deltas
            if (parsedLiftArea[index] > 0f)
            {
                // If lift area delta
                currLiftArea += parsedLiftArea[index];
                // Then check drag area delta
                // if drag area delta exists, then
                // apply it, otherwise just apply
                // lift area delta
                if (parsedDragArea[index] > 0f)
                    currDragArea += parsedDragArea[index];
                else
                    currDragArea += parsedLiftArea[index];
            }
            else if (parsedDragArea[index] > 0f)
                // If drag area delta, apply it
                currDragArea += parsedDragArea[index];

            // Apply any maxTorqueAero delta
            if (parsedMaxTorqueAero[index] > 0f)
                currMaxTorqueAero += parsedMaxTorqueAero[index];
        }

        IEnumerator CruiseAnimRoutine()
        {
            yield return new WaitForSecondsFixed(cruiseDeployTime);
            if (cruiseStates == null)
            {
                if (BDArmorySettings.DEBUG_MISSILES) Debug.LogWarning("[BDArmory.MissileLauncher]: deployStates was null, aborting AnimRoutine.");
                yield break;
            }

            if (!string.IsNullOrEmpty(cruiseAnimationName))
            {
                deployed = true;

                applyDeployedLiftDrag(true);
                MissileGuidance.setupTorqueAoALimit(this, currLiftArea, currDragArea);

                using (var anim = cruiseStates.AsEnumerable().GetEnumerator())
                    while (anim.MoveNext())
                    {
                        if (anim.Current == null) continue;
                        anim.Current.enabled = true;
                        anim.Current.speed = 1;
                    }
            }
        }
        IEnumerator FlightAnimRoutine()
        {
            if (animStates == null)
            {
                if (BDArmorySettings.DEBUG_MISSILES) Debug.LogWarning("[BDArmory.MissileLauncher]: animStates was null, aborting AnimRoutine.");
                yield break;
            }

            if (!string.IsNullOrEmpty(flightAnimationName))
            {
                using (var anim = animStates.AsEnumerable().GetEnumerator())
                    while (anim.MoveNext())
                    {
                        if (anim.Current == null) continue;
                        anim.Current.enabled = true;
                        if (!OneShotAnim)
                        {
                            anim.Current.wrapMode = WrapMode.Loop;
                        }
                        anim.Current.speed = 1;
                    }
            }
        }
        IEnumerator updateCrashTolerance()
        {
            yield return new WaitForSecondsFixed(0.5f); //wait half sec after boost motor fires, then set crashTolerance to 1. Torps have already waited until splashdown before this is called.
            part.crashTolerance = 1;
            if (useSimpleDragTemp)
            {
                yield return new WaitForSecondsFixed((clearanceLength * 1.2f) / 2);
                part.dragModel = Part.DragModel.DEFAULT;
                useSimpleDragTemp = false;
            }
            var childColliders = part.GetComponentsInChildren<Collider>(includeInactive: false);
            foreach (var col in childColliders)
                col.enabled = true;

        }
        IEnumerator BoostRoutine()
        {
            if (weaponClass == WeaponClasses.SLW && FlightGlobals.getAltitudeAtPos(vessel.CoM) > 0)
            {
                yield return new WaitUntilFixed(() => vessel == null || vessel.LandedOrSplashed);//don't start torpedo thrust until underwater
                if (vessel == null || vessel.Landed) Detonate(); //dropping torpedoes over land is just going to turn them into heavy, expensive bombs...
            }
            if (useFuel)
            {
                burnRate = boostTime > 0 ? boosterFuelMass / boostTime * Time.fixedDeltaTime : 0;
                burnedFuelMass = 0f;
            }

            StartBoost();
            StartCoroutine(updateCrashTolerance());

            var wait = new WaitForFixedUpdate();
            float boostStartTime = Time.time;
            while (Time.time - boostStartTime < boostTime || (useFuel && burnedFuelMass < boosterFuelMass))
            {
                //light, sound & particle fx
                //sound
                if (!BDArmorySetup.GameIsPaused)
                {
                    if (!audioSource.isPlaying)
                    {
                        audioSource.Play();
                    }
                }
                else if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }

                //thrust
                if (useFuel && burnRate > 0)
                {
                    //if (boosterFuelMass - burnedFuelMass < burnRate * Throttle)
                    //{
                    //    Throttle = (boosterFuelMass - burnedFuelMass) / burnRate;
                    //    burnedFuelMass = boosterFuelMass;
                    //}
                    //else
                    //{
                    burnedFuelMass = Mathf.Min(burnedFuelMass + Throttle * burnRate, boosterFuelMass); // Impulse conservation code was showing issues
                    //}
                }

                audioSource.volume = Throttle;

                //particleFx
                using (var emitter = boostEmitters.GetEnumerator())
                    while (emitter.MoveNext())
                    {
                        if (emitter.Current == null) continue;
                        //if (!hasRCS)
                        //{
                        //    emitter.Current.sizeGrow = Mathf.Lerp(emitter.Current.sizeGrow, 0, 20 * Time.deltaTime);
                        //}
                        if (Throttle == 0 || thrust == 0)
                            emitter.Current.emit = false;
                        else
                            emitter.Current.emit = true;
                    }

                using (var gpe = boostGaplessEmitters.GetEnumerator())
                    while (gpe.MoveNext())
                    {
                        if (gpe.Current == null) continue;
                        if ((!vessel.InVacuum() && Throttle > 0) && weaponClass != WeaponClasses.SLW || (weaponClass == WeaponClasses.SLW && FlightGlobals.getAltitudeAtPos(vessel.CoM) < 0)) //#710
                        {
                            if (Throttle == 0 || thrust == 0)
                                gpe.Current.emit = false;
                            else
                            {
                                gpe.Current.emit = true;
                                gpe.Current.pEmitter.worldVelocity = 2 * ParticleTurbulence.flareTurbulence;
                            }
                        }
                        else
                        {
                            gpe.Current.emit = false;
                        }
                    }

                if (spoolEngine)
                {
                    currentThrust = Mathf.MoveTowards(currentThrust, thrust, thrust / 10);
                }

                yield return wait;
            }
            EndBoost();
        }

        void StartBoost()
        {
            MissileState = MissileStates.Boost;

            if (audioSource == null || sfAudioSource == null) SetupAudio();
            if (boostAudio)
            {
                audioSource.clip = boostAudio;
            }
            else if (thrustAudio)
            {
                audioSource.clip = thrustAudio;
            }
            audioSource.volume = Throttle;

            if (BDArmorySettings.LightFX)
            {
                using (var light = gameObject.GetComponentsInChildren<Light>().AsEnumerable().GetEnumerator())
                    while (light.MoveNext())
                    {
                        if (light.Current == null) continue;
                        light.Current.intensity = 1.5f;
                    }
            }

            if (!spoolEngine)
            {
                currentThrust = thrust;
            }

            if (string.IsNullOrEmpty(boostTransformName))
            {
                boostEmitters = pEmitters;
                if (hasRCS && rcsTransforms != null) boostEmitters.RemoveAll(pe => rcsTransforms.Contains(pe));
                if (hasRCS && forwardRCS.Any())
                    foreach (var pe in forwardRCS)
                        if (!boostEmitters.Contains(pe)) boostEmitters.Add(pe);
                boostGaplessEmitters = gaplessEmitters;
            }

            using (var emitter = boostEmitters.GetEnumerator())
                while (emitter.MoveNext())
                {
                    if (emitter.Current == null) continue;
                    emitter.Current.emit = true;
                }

            if (!(thrust > 0)) return;
            sfAudioSource.PlayOneShot(SoundUtils.GetAudioClip("BDArmory/Sounds/launch"));
            RadarWarningReceiver.WarnMissileLaunch(vessel.CoM, transform.forward, TargetingMode == TargetingModes.Radar);
        }

        void EndBoost()
        {
            using (var emitter = boostEmitters.GetEnumerator())
                while (emitter.MoveNext())
                {
                    if (emitter.Current == null) continue;
                    emitter.Current.emit = false;
                }

            using (var gEmitter = boostGaplessEmitters.GetEnumerator())
                while (gEmitter.MoveNext())
                {
                    if (gEmitter.Current == null) continue;
                    gEmitter.Current.emit = false;
                }

            if (useFuel) burnedFuelMass = boosterFuelMass;

            if (parsedMaxTorque[1] > 0)
                currMaxTorque = parsedMaxTorque[1];

            if (parsedSteerMult[1] > 0)
                currSteerMult = parsedSteerMult[1];

            if (decoupleBoosters)
            {
                // We only apply any lift/drag area changes if parsedLiftArea[1] changes
                if (parsedLiftArea[1] > 0)
                {
                    currLiftArea = parsedLiftArea[1];
                    currDragArea = parsedDragArea[1];

                    if (currDragArea < 0)
                    {
                        if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: decoupleBoosters missile {shortName}: setting default dragArea to liftArea {currLiftArea}:");
                        currDragArea = currLiftArea;
                    }

                    currMaxTorqueAero = parsedMaxTorqueAero[1];
                    if (currMaxTorqueAero < 0)
                        currMaxTorqueAero = 0f;

                    if (deployed && deployedLiftInCruise)
                        applyDeployedLiftDrag();

                    MissileGuidance.setupTorqueAoALimit(this, currLiftArea, currDragArea);
                }

                boostersDecoupled = true;
                using (var booster = boosters.GetEnumerator())
                    while (booster.MoveNext())
                    {
                        if (booster.Current == null) continue;
                        booster.Current.AddComponent<DecoupledBooster>().DecoupleBooster(part.rb.velocity, boosterDecoupleSpeed);
                    }
            }

            if (cruiseDelay > 0 || cruiseRangeTrigger > 0)
            {
                currentThrust = 0;
            }
        }

        IEnumerator CruiseRoutine()
        {
            float massToBurn = 0;
            if (useFuel)
            {
                burnRate = cruiseTime > 0 ? cruiseFuelMass / cruiseTime * Time.fixedDeltaTime : 0;
                massToBurn = boosterFuelMass + cruiseFuelMass;
            }
            StartCruise();
            var wait = new WaitForFixedUpdate();
            float cruiseStartTime = Time.time;
            while (Time.time - cruiseStartTime < cruiseTime || (useFuel && burnedFuelMass < massToBurn))
            {
                if (!BDArmorySetup.GameIsPaused)
                {
                    if (!audioSource.isPlaying || audioSource.clip != thrustAudio)
                    {
                        audioSource.clip = thrustAudio;
                        audioSource.Play();
                    }
                }
                else if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }

                //Thrust
                if (useFuel && burnRate > 0)
                {
                    //if (massToBurn - burnedFuelMass < burnRate * Throttle)
                    //{
                    //    Throttle = (massToBurn - burnedFuelMass) / burnRate;
                    //    burnedFuelMass = massToBurn;
                    //}
                    //else
                    //{
                    burnedFuelMass = Mathf.Min(burnedFuelMass + Throttle * burnRate, massToBurn); // Other code was causing issues
                    //}
                }

                audioSource.volume = Throttle;

                //particleFx
                using (var emitter = pEmitters.GetEnumerator())
                    while (emitter.MoveNext())
                    {
                        if (emitter.Current == null) continue;
                        /*
                        if (!hasRCS)
                        {
                            emitter.Current.sizeGrow = Mathf.Lerp(emitter.Current.sizeGrow, 0, 20 * Time.deltaTime); //uh, why? this turns reasonable missileFX into giant doom plumes
                        }
                        emitter.Current.maxSize = Mathf.Clamp01(Throttle / Mathf.Clamp((float)vessel.atmDensity, 0.2f, 1f));
                        */
                        if (weaponClass != WeaponClasses.SLW || (weaponClass == WeaponClasses.SLW && FlightGlobals.getAltitudeAtPos(vessel.CoM) < 0)) //#710
                        {
                            if (Throttle == 0 || cruiseThrust == 0)
                                emitter.Current.emit = false;
                            else
                                emitter.Current.emit = true;
                        }
                        else
                        {
                            emitter.Current.emit = false; // #710, shut down thrust FX for torps out of water
                        }
                    }

                using (var gpe = gaplessEmitters.GetEnumerator())
                    while (gpe.MoveNext())
                    {
                        if (gpe.Current == null) continue;
                        if (weaponClass != WeaponClasses.SLW || (weaponClass == WeaponClasses.SLW && FlightGlobals.getAltitudeAtPos(vessel.CoM) < 0)) //#710
                        {
                            if (Throttle == 0 || cruiseThrust == 0)
                                gpe.Current.emit = false;
                            else
                            {
                                //gpe.Current.pEmitter.maxSize = Mathf.Clamp01(Throttle / Mathf.Clamp((float)vessel.atmDensity, 0.2f, 1f));
                                gpe.Current.emit = true;
                                gpe.Current.pEmitter.worldVelocity = 2 * ParticleTurbulence.flareTurbulence;
                            }
                        }
                        else
                        {
                            gpe.Current.emit = false;
                        }
                    }

                if (spoolEngine)
                {
                    currentThrust = Mathf.MoveTowards(currentThrust, cruiseThrust, cruiseThrust / 10);
                }

                yield return wait;
            }

            EndCruise();
        }

        void StartCruise()
        {
            MissileState = MissileStates.Cruise;

            if (audioSource == null) SetupAudio();
            if (thrustAudio)
            {
                audioSource.clip = thrustAudio;
            }

            currentThrust = spoolEngine ? 0 : cruiseThrust;

            using (var pEmitter = pEmitters.GetEnumerator())
                while (pEmitter.MoveNext())
                {
                    if (pEmitter.Current == null) continue;
                    EffectBehaviour.AddParticleEmitter(pEmitter.Current);
                    pEmitter.Current.emit = true;
                }

            using (var gEmitter = gaplessEmitters.GetEnumerator())
                while (gEmitter.MoveNext())
                {
                    if (gEmitter.Current == null) continue;
                    EffectBehaviour.AddParticleEmitter(gEmitter.Current.pEmitter);
                    gEmitter.Current.emit = true;
                }

            if (!hasRCS) return;
            foreach (var pe in forwardRCS)
                pe.emit = false;
            audioSource.Stop();
        }

        void EndCruise()
        {
            MissileState = MissileStates.PostThrust;

            currentThrust = 0f;

            if (useFuel) burnedFuelMass = cruiseFuelMass + boosterFuelMass;

            // If we specify a post-thrust maxTorque (I.E. TVC)
            if (parsedMaxTorque[2] > 0)
                currMaxTorque = parsedMaxTorque[2];

            using (IEnumerator<Light> light = gameObject.GetComponentsInChildren<Light>().AsEnumerable().GetEnumerator())
                while (light.MoveNext())
                {
                    if (light.Current == null) continue;
                    light.Current.intensity = 0;
                }

            StartCoroutine(FadeOutAudio());
            StartCoroutine(FadeOutEmitters());
        }

        IEnumerator FadeOutAudio()
        {
            if (thrustAudio && audioSource.isPlaying)
            {
                while (audioSource.volume > 0 || audioSource.pitch > 0)
                {
                    audioSource.volume = Mathf.Lerp(audioSource.volume, 0, 5 * Time.deltaTime);
                    audioSource.pitch = Mathf.Lerp(audioSource.pitch, 0, 5 * Time.deltaTime);
                    yield return null;
                }
            }
        }

        IEnumerator FadeOutEmitters()
        {
            float fadeoutStartTime = Time.time;
            while (Time.time - fadeoutStartTime < 5)
            {
                /*
                using (var pe = pEmitters.GetEnumerator())
                    while (pe.MoveNext())
                    {
                        if (pe.Current == null) continue;
                        pe.Current.maxEmission = Mathf.FloorToInt(pe.Current.maxEmission * 0.8f);
                        pe.Current.minEmission = Mathf.FloorToInt(pe.Current.minEmission * 0.8f);
                    }
                */
                using (var gpe = gaplessEmitters.GetEnumerator())
                    while (gpe.MoveNext())
                    {
                        if (gpe.Current == null) continue;
                        //gpe.Current.pEmitter.maxSize = Mathf.MoveTowards(gpe.Current.pEmitter.maxSize, 0, 0.005f);
                        //gpe.Current.pEmitter.minSize = Mathf.MoveTowards(gpe.Current.pEmitter.minSize, 0, 0.008f);
                        gpe.Current.pEmitter.worldVelocity = ParticleTurbulence.Turbulence;
                    }
                yield return new WaitForFixedUpdate();
            }

            yield return new WaitForFixedUpdate();
            using (var pe2 = pEmitters.GetEnumerator())
                while (pe2.MoveNext())
                {
                    if (pe2.Current == null) continue;
                    pe2.Current.emit = false;
                }

            using (var gpe2 = gaplessEmitters.GetEnumerator())
                while (gpe2.MoveNext())
                {
                    if (gpe2.Current == null) continue;
                    gpe2.Current.emit = false;
                }
        }

        [KSPField]
        public float beamCorrectionFactor;

        [KSPField]
        public float beamCorrectionDamping;

        Ray previousBeam;

        void BeamRideGuidance()
        {
            if (!targetingPod)
            {
                guidanceActive = false;
                return;
            }

            if (RadarUtils.TerrainCheck(targetingPod.cameraParentTransform.position, vessel.CoM))
            {
                guidanceActive = false;
                return;
            }
            Ray laserBeam = new Ray(targetingPod.cameraParentTransform.position + (targetingPod.vessel.Velocity() * Time.fixedDeltaTime), targetingPod.targetPointPosition - targetingPod.cameraParentTransform.position);
            Vector3 target = MissileGuidance.GetBeamRideTarget(laserBeam, vessel.CoM, vessel.Velocity(), beamCorrectionFactor, beamCorrectionDamping, (TimeIndex > 0.25f ? previousBeam : laserBeam));
            previousBeam = laserBeam;
            DrawDebugLine(vessel.CoM, target);
            DoAero(target);
        }

        void CruiseGuidance()
        {
            if (this._guidance == null)
            {
                this._guidance = new CruiseGuidance(this);
            }

            Vector3 cruiseTarget = TargetPosition;

            if (FlightGlobals.currentMainBody.ocean && targetVessel != null)
            {
                if (targetVessel.Vessel.radarAltitude < 0)
                    cruiseTarget = cruiseTarget - targetVessel.Vessel.up * targetVessel.Vessel.radarAltitude;
            }

            cruiseTarget = this._guidance.GetDirection(this, cruiseTarget, TargetVelocity);

            Vector3 upDirection = vessel.upAxis;

            //axial rotation
            if (rotationTransform)
            {
                Quaternion originalRotation = transform.rotation;
                Quaternion originalRTrotation = rotationTransform.rotation;
                transform.rotation = Quaternion.LookRotation(transform.forward, upDirection);
                rotationTransform.rotation = originalRTrotation;
                Vector3 lookUpDirection = (cruiseTarget - vessel.CoM).ProjectOnPlanePreNormalized(transform.forward) * 100;
                lookUpDirection = transform.InverseTransformPoint(lookUpDirection + vessel.CoM);

                lookUpDirection = new Vector3(lookUpDirection.x, 0, 0);
                lookUpDirection += 10 * Vector3.up;

                rotationTransform.localRotation = Quaternion.Lerp(rotationTransform.localRotation, Quaternion.LookRotation(Vector3.forward, lookUpDirection), 0.04f);
                Quaternion finalRotation = rotationTransform.rotation;
                transform.rotation = originalRotation;
                rotationTransform.rotation = finalRotation;

                vesselReferenceTransform.rotation = Quaternion.LookRotation(-rotationTransform.up, rotationTransform.forward);
            }
            DoAero(cruiseTarget);
            CheckMiss();
        }

        void AAMGuidance()
        {
            Vector3 aamTarget = TargetPosition;
            float currgLimit = -1f;
            float currAoALimit = -1f;

            if (TargetAcquired)
            {
                if (warheadType == WarheadTypes.ContinuousRod) //Have CR missiles target slightly above target to ensure craft caught in planar blast AOE
                {
                    TargetPosition += VectorUtils.GetUpDirection(TargetPosition) * (blastRadius > 0f ? Mathf.Min(blastRadius / 3f, DetonationDistance / 3f) : 5f);
                }
                DrawDebugLine(vessel.CoM + (part.rb.velocity * Time.fixedDeltaTime), TargetPosition);

                float timeToImpact;
                switch (GuidanceMode)
                {
                    case GuidanceModes.APN:
                        {
                            float tempPronavGain = pronavGain > 0 ? pronavGain : pronavGainCurve.Evaluate(Vector3.Distance(TargetPosition, vessel.CoM));

                            aamTarget = MissileGuidance.GetAPNTarget(TargetPosition, TargetVelocity, TargetAcceleration, vessel, tempPronavGain, out timeToImpact, out currgLimit);
                            TimeToImpact = timeToImpact;
                            break;
                        }

                    case GuidanceModes.PN: // Pro-Nav
                        {
                            float tempPronavGain = pronavGain > 0 ? pronavGain : pronavGainCurve.Evaluate(Vector3.Distance(TargetPosition, vessel.CoM));

                            aamTarget = MissileGuidance.GetPNTarget(TargetPosition, TargetVelocity, vessel, tempPronavGain, out timeToImpact, out currgLimit);
                            TimeToImpact = timeToImpact;
                            break;
                        }
                    case GuidanceModes.AAMLoft:
                        {
                            float targetAlt = FlightGlobals.getAltitudeAtPos(TargetPosition);

                            if (TimeToImpact == float.PositiveInfinity)
                            {
                                // If the missile is not in a vaccuum, is above LoftMinAltitude and has an angle to target below the climb angle (or 90 - climb angle if climb angle > 45) (in this case, since it's angle from the vertical the check is if it's > 90f - LoftAngle) and is either is at a lower altitude than targetAlt + LoftAltitudeAdvMax or further than LoftRangeOverride, then loft.
                                if (!vessel.InVacuum() && (vessel.altitude >= LoftMinAltitude) && Vector3.Angle(TargetPosition - vessel.CoM, vessel.upAxis) > Mathf.Min(LoftAngle, 90f - LoftAngle) && ((vessel.altitude - targetAlt <= LoftAltitudeAdvMax) || (TargetPosition - vessel.CoM).sqrMagnitude > (LoftRangeOverride * LoftRangeOverride))) loftState = LoftStates.Boost;
                                else loftState = LoftStates.Terminal;
                            }

                            float tempPronavGain = pronavGain > 0 ? pronavGain : pronavGainCurve.Evaluate(Vector3.Distance(TargetPosition, vessel.CoM));

                            //aamTarget = MissileGuidance.GetAirToAirLoftTarget(TargetPosition, TargetVelocity, TargetAcceleration, vessel, targetAlt, LoftMaxAltitude, LoftRangeFac, LoftAltComp, LoftVelComp, LoftAngle, LoftTermAngle, terminalHomingRange, ref loftState, out float currTimeToImpact, out float rangeToTarget, optimumAirspeed);
                            aamTarget = MissileGuidance.GetAirToAirLoftTarget(TargetPosition, TargetVelocity, TargetAcceleration, vessel, targetAlt, LoftMaxAltitude, LoftRangeFac, LoftVertVelComp, LoftVelComp, LoftAngle, LoftTermAngle, terminalHomingRange, ref loftState, out float currTimeToImpact, out currgLimit, out float rangeToTarget, homingModeTerminal, tempPronavGain, optimumAirspeed);

                            //float fac = (1 - (rangeToTarget - terminalHomingRange - 100f) / Mathf.Clamp(terminalHomingRange * 4f, 5000f, 25000f));

                            //if (loftState > LoftStates.Boost)
                            //    maxAoA = Mathf.Clamp(initMaxAoA * fac, 4f, initMaxAoA);
                            if (loftState == LoftStates.Midcourse)
                                currAoALimit = 30f;

                            TimeToImpact = currTimeToImpact;

                            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: AAM Loft TTGO: [{TimeToImpact:G3}]. Currently State: {loftState}. Fly to: [{aamTarget}]. Target Position: [{TargetPosition}]. Max AoA: [{maxAoA:G3}]");
                            break;
                        }
                    case GuidanceModes.AAMPure:
                        {
                            TimeToImpact = Vector3.Distance(TargetPosition, vessel.CoM) / Mathf.Max((float)vessel.srfSpeed, optimumAirspeed);
                            aamTarget = TargetPosition;
                            break;
                        }
                    /* Case GuidanceModes.AAMHybrid:
{
                            aamTarget = MissileGuidance.GetAirToAirHybridTarget(TargetPosition, TargetVelocity, TargetAcceleration, vessel, terminalHomingRange, out timeToImpact, homingModeTerminal, pronavGain, optimumAirspeed);
                            TimeToImpact = timeToImpact;
                            break;
                        }
                    */
                    case GuidanceModes.AAMLead:
                        {
                            aamTarget = MissileGuidance.GetAirToAirTarget(TargetPosition, TargetVelocity, TargetAcceleration, vessel, out timeToImpact, optimumAirspeed);
                            TimeToImpact = timeToImpact;
                            break;
                        }

                    case GuidanceModes.Kappa:
                        {
                            aamTarget = MissileGuidance.GetKappaTarget(TargetPosition, TargetVelocity, this, MissileState == MissileStates.PostThrust ? 0f : currentThrust * Throttle, kappaAngle, LoftRangeFac, LoftVertVelComp, FlightGlobals.getAltitudeAtPos(TargetPosition), terminalHomingRange, LoftAngle, LoftTermAngle, LoftRangeOverride, LoftMaxAltitude, out timeToImpact, out currgLimit, ref loftState);
                            TimeToImpact = timeToImpact;
                            break;
                        }

                    case GuidanceModes.Weave:
                        {
                            aamTarget = MissileGuidance.GetWeaveTarget(TargetPosition, TargetVelocity, vessel, ref WeaveVerticalG, ref WeaveHorizontalG, WeaveRandomRange, WeaveFrequency, WeaveTerminalAngle, WeaveFactor, WeaveUseAGMDescentRatio, agmDescentRatio, ref WeaveOffset, ref WeaveStart, ref WeaveAlt, out timeToImpact, out currgLimit);
                            TimeToImpact = timeToImpact;
                            break;
                        }
                }

                if (Vector3.Angle(aamTarget - vessel.CoM, transform.forward) > maxOffBoresight * 0.75f)
                {
                    aamTarget = TargetPosition;
                }

                //proxy detonation
                var distThreshold = 0.5f * GetBlastRadius();
                if (proxyDetonate && !DetonateAtMinimumDistance && ((TargetPosition + (TargetVelocity * Time.fixedDeltaTime)) - (vessel.CoM)).sqrMagnitude < distThreshold * distThreshold)
                {
                    //part.Destroy(); //^look into how this interacts with MissileBase.DetonationState
                    // - if the missile is still within the notSafe status, the missile will delete itself, else, the checkProximity state of DetonationState would trigger before the missile reaches the 1/2 blastradius.
                    // would only trigger if someone set the detonation distance override to something smallerthan 1/2 blst radius, for some reason
                    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher] ProxiDetonate triggered");
                    Detonate();
                }
            }
            else
            {
                aamTarget = vessel.CoM + (2000f * vessel.Velocity().normalized);
            }

            if (TimeIndex > dropTime + 0.25f)
            {
                DoAero(aamTarget, currgLimit, currAoALimit);
                CheckMiss();
            }

        }

        void AGMGuidance()
        {
            if (TargetingMode != TargetingModes.Gps)
            {
                if (TargetAcquired)
                {
                    //lose lock if seeker reaches gimbal limit
                    float targetViewAngle = Vector3.Angle(transform.forward, TargetPosition - vessel.CoM);

                    if (targetViewAngle > maxOffBoresight)
                    {
                        if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher]: AGM Missile guidance failed - target out of view");
                        guidanceActive = false;
                    }
                    CheckMiss();
                }
                else
                {
                    if (TargetingMode == TargetingModes.Laser)
                    {
                        //keep going straight until found laser point
                        TargetPosition = laserStartPosition + (20000 * startDirection);
                    }
                }
            }

            Vector3 targetPosTemp = TargetPosition;

            if (FlightGlobals.currentMainBody.ocean && targetVessel != null)
            {
                if (targetVessel.Vessel.radarAltitude < 0)
                    targetPosTemp = targetPosTemp - targetVessel.Vessel.up * targetVessel.Vessel.radarAltitude;
            }

            Vector3 agmTarget = MissileGuidance.GetAirToGroundTarget(targetPosTemp, TargetVelocity, vessel, agmDescentRatio);
            DoAero(agmTarget);
        }

        void SLWGuidance()
        {
            Vector3 SLWTarget;
            float runningDepth = Mathf.Min(-3, (float)FlightGlobals.getAltitudeAtPos(TargetPosition));
            Vector3 upDir = vessel.upAxis;
            if (TargetAcquired)
            {
                //DrawDebugLine(transform.position + (part.rb.velocity * Time.fixedDeltaTime), TargetPosition);
                float timeToImpact;

                SLWTarget = MissileGuidance.GetAirToAirTarget(TargetPosition, TargetVelocity, TargetAcceleration, vessel, out timeToImpact, optimumAirspeed);
                if (Vector3.Angle(SLWTarget - vessel.CoM, transform.forward) > maxOffBoresight * 0.75f)
                {
                    SLWTarget = TargetPosition;
                }
                SLWTarget = vessel.CoM + (SLWTarget - vessel.CoM).normalized * 100;
                SLWTarget = (SLWTarget - ((float)FlightGlobals.getAltitudeAtPos(SLWTarget) * upDir)) + upDir * runningDepth;
                TimeToImpact = timeToImpact;

                //proxy detonation
                var distThreshold = 0.5f * GetBlastRadius();
                if (proxyDetonate && !DetonateAtMinimumDistance && ((TargetPosition + (TargetVelocity * Time.fixedDeltaTime)) - (vessel.CoM)).sqrMagnitude < distThreshold * distThreshold)
                {
                    Detonate(); //ends up the same as part.Destroy, except it doesn't trip the hasDied flag for clustermissiles
                }
            }
            else
            {
                SLWTarget = TargetPosition; //head to last known contact and then begin circling
                SLWTarget = vessel.CoM + (SLWTarget - vessel.CoM.normalized) * 100;
                SLWTarget = (SLWTarget - ((float)FlightGlobals.getAltitudeAtPos(SLWTarget) * upDir)) + upDir * runningDepth;
            }
            DrawDebugLine(vessel.CoM, SLWTarget, Color.blue);
            //allow inverse contRod-style target offset for srf targets for 'under-the-keel' proximity detonation? or at least not having the torps have a target alt of 0 (and thus be vulnerable to surface PD?)
            if (TimeIndex > dropTime + 0.25f)
            {
                DoAero(SLWTarget);
            }

            CheckMiss();

        }

        void DoAero(Vector3 targetPosition, float currgLimit = -1f, float currAoALimit = -1f)
        {
            if (gLimit > 0f)
            {
                if (currgLimit < 0f)
                    currgLimit = gLimit;
                else
                {
                    currgLimit = Mathf.Min(currgLimit, gLimit);
                    currgLimit += Mathf.Min(0.15f * currgLimit, 2f);
                }
            }
            else
                currgLimit = -1f;

            if (currAoALimit < 0f)
                currAoALimit = maxAoA;
            else
                currAoALimit = Mathf.Min(currAoALimit, maxAoA);

            if (currgLimit > 0f)
            {
                currAoALimit = MissileGuidance.getGLimit(this, MissileState == MissileStates.PostThrust ? 0f : currentThrust * Throttle, currgLimit, gMargin, currAoALimit);
                //if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: maxAoA: {maxAoA}, currAoALimit: {currAoALimit}, currgLimit: {currgLimit}");
            }

            aeroTorque = MissileGuidance.DoAeroForces(this, targetPosition, currLiftArea, currDragArea, controlAuthority * currSteerMult, aeroTorque, finalMaxTorque, currMaxTorqueAero, currAoALimit, MissileGuidance.DefaultLiftCurve, MissileGuidance.DefaultDragCurve);
        }

        void AGMBallisticGuidance()
        {
            DoAero(CalculateAGMBallisticGuidance(this, TargetPosition));
        }

        void OrbitalGuidance(float turnRateDPS)
        {
            Vector3 orbitalTarget;
            if (TargetAcquired)
            {
                float guidance_thrust = currentThrust;
                if (currentThrust == 0 && cruiseDelay > 0 && (TimeIndex > dropTime + boostTime) && (TimeIndex < dropTime + boostTime + cruiseDelay)) // If in the cruiseDelay, fake thrust to avoid discontinuities in the guidance
                    guidance_thrust = Mathf.Lerp(thrust, cruiseThrust, (TimeIndex - (dropTime + boostTime)) / cruiseDelay);

                if (!hasRCS) // Use thrust to kill relative velocity
                {
                    Vector3 targetVector = TargetPosition - vessel.CoM;
                    Vector3 acceleration = guidance_thrust / part.mass * GetForwardTransform();
                    Vector3 relVel = TargetVelocity - vessel.Velocity();
                    float timeToImpact = AIUtils.TimeToCPA(targetVector, relVel, TargetAcceleration - acceleration, 30);
                    orbitalTarget = AIUtils.PredictPosition(targetVector, relVel, TargetAcceleration - 0.5f * acceleration, timeToImpact);
                }
                else // Use thrust to kill relative velocity early, with RCS for later adjustments
                {
                    Vector3 targetVector = TargetPosition - vessel.CoM;
                    Vector3 relVel = vessel.Velocity() - TargetVelocity;
                    Vector3 tvNorm = targetVector.normalized;
                    float timeToImpact = BDAMath.SolveTime(targetVector.magnitude, guidance_thrust / part.mass, Vector3.Dot(relVel, tvNorm));
                    Vector3 lead = -timeToImpact * relVel;
                    float t = (targetVessel && targetVessel.isMissile) ? Vector3.Dot(targetVector + lead, tvNorm) / (targetVector + lead).magnitude : relVel.sqrMagnitude > 0 ? Vector3.Dot(relVel, tvNorm) / relVel.magnitude : 1;
                    orbitalTarget = Vector3.Slerp(TargetPosition + lead, TargetPosition, t);
                }

                // Clamp target position to max off boresight
                float angleToTarget = Vector3.Angle(TargetPosition - vessel.CoM, orbitalTarget - vessel.CoM);
                if (angleToTarget > maxOffBoresight)
                {
                    orbitalTarget = vessel.CoM + Vector3.RotateTowards(TargetPosition - vessel.CoM, orbitalTarget - vessel.CoM, maxOffBoresight * Mathf.Deg2Rad, 0f);
                }
            }
            else
                orbitalTarget = vessel.CoM + (2000f * vessel.Velocity().normalized);

            // In vacuum, with RCS, point towards target shortly after launch to minimize wasted delta-V
            // During this maneuver, check that we have cleared any obstacles before throttling up
            orbitalTarget = VacuumClearanceManeuver(orbitalTarget, vessel.CoM, hasRCS, vacuumSteerable);
            if (Throttle == 0)
                turnRateDPS *= 15f;

            // If in atmosphere, apply drag
            if (!vessel.InVacuum() && vessel.srfSpeed > 0f)
            {
                Rigidbody rb = part.rb;
                if (rb != null && rb.mass > 0)
                {
                    double airDensity = vessel.atmDensity;
                    double airSpeed = vessel.srfSpeed;
                    Vector3d velocity = vessel.Velocity();
                    Vector3 CoL = new Vector3(0, 0, -1f);
                    float AoA = Mathf.Clamp(Vector3.Angle(part.transform.forward, velocity), 0, 90);
                    double dragForce = 0.5 * airDensity * airSpeed * airSpeed * currDragArea * BDArmorySettings.GLOBAL_DRAG_MULTIPLIER * Mathf.Max(MissileGuidance.DefaultDragCurve.Evaluate(AoA), 0f);
                    rb.AddForceAtPosition((float)dragForce * -velocity.normalized,
                        part.transform.TransformPoint(part.CoMOffset + CoL));
                }
            }

            part.transform.rotation = Quaternion.RotateTowards(part.transform.rotation, Quaternion.LookRotation(orbitalTarget - vessel.CoM, TargetVelocity), turnRateDPS * Time.fixedDeltaTime);
            if (TimeIndex > dropTime + 0.25f)
                CheckMiss();

            DrawDebugLine(vessel.CoM + (part.rb.velocity * Time.fixedDeltaTime), orbitalTarget);
        }

        public override void Detonate()
        {
            if (HasExploded || FuseFailed || !HasFired) return;

            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher]: Detonate Triggered");

            BDArmorySetup.numberOfParticleEmitters--;
            HasExploded = true;
            /*
            if (targetVessel != null)
            {
                using (var wpm = VesselModuleRegistry.GetModules<MissileFire>(targetVessel).GetEnumerator())
                    while (wpm.MoveNext())
                    {
                        if (wpm.Current == null) continue;
                        wpm.Current.missileIsIncoming = false; //handled by attacked vessel
                    }
            }
            */
            if (SourceVessel == null) SourceVessel = vessel;
            if (multiLauncher && multiLauncher.isClusterMissile)
            {
                if (!HasDied)
                {
                    if (fairings.Count > 0)
                    {
                        using (var fairing = fairings.GetEnumerator())
                            while (fairing.MoveNext())
                            {
                                if (fairing.Current == null) continue;
                                fairing.Current.AddComponent<DecoupledBooster>().DecoupleBooster(part.rb.velocity, boosterDecoupleSpeed);
                            }
                    }
                    multiLauncher.Team = Team;
                    multiLauncher.fireMissile(true);
                }
            }
            else
            {
                if (warheadType == WarheadTypes.Standard || warheadType == WarheadTypes.ContinuousRod ||
                    warheadType == WarheadTypes.Custom ||
                    warheadType == WarheadTypes.CustomStandard || warheadType == WarheadTypes.CustomContinuous)
                {
                    /*
                    if (warheadType == WarheadTypes.Standard || warheadType == WarheadTypes.ContinuousRod ||
                    warheadType == WarheadTypes.CustomStandard || warheadType == WarheadTypes.CustomContinuous)
                    {
                        var tnt = part.FindModuleImplementing<BDExplosivePart>();
                        tnt.DetonateIfPossible();
                        FuseFailed = tnt.fuseFailed;
                        guidanceActive = false;
                        if (FuseFailed)
                            HasExploded = false;
                    }

                    if (warheadType == WarheadTypes.Custom || warheadType == WarheadTypes.CustomStandard || warheadType == WarheadTypes.CustomContinuous)
                    {
                        var warhead = part.FindModuleImplementing<BDCustomWarhead>();
                        warhead.DetonateIfPossible();
                        FuseFailed = warhead.fuseFailed;
                        guidanceActive = false;
                        if (FuseFailed)
                            HasExploded = false;
                    }
                    */
                    var tntList = part.FindModulesImplementing<BDWarheadBase>();
                    foreach (BDWarheadBase tnt in tntList)
                    {
                        tnt.DetonateIfPossible();
                        FuseFailed = tnt.fuseFailed || FuseFailed;
                    }
                    guidanceActive = false;
                    if (FuseFailed)
                        HasExploded = false;
                }
                else if (warheadType == WarheadTypes.Nuke)
                {
                    var U235 = part.FindModuleImplementing<BDModuleNuke>();
                    U235.Detonate();
                }
                else if (warheadType == WarheadTypes.EMP || warheadType == WarheadTypes.Legacy) // EMP/really old legacy missiles using BlastPower
                {
                    Vector3 position = transform.position;//+rigidbody.velocity*Time.fixedDeltaTime;
                    ExplosionFx.CreateExplosion(position, blastPower, explModelPath, explSoundPath, ExplosionSourceType.Missile, 0, part, SourceVessel.vesselName, Team.Name, GetShortName(), default(Vector3), -1, warheadType == WarheadTypes.EMP, part.mass * 1000);
                }
                else if (warheadType == WarheadTypes.Kinetic) // Missile will usually just phase through target at high speeds (even with ContinuousCollisions mod), so fake effects using an explosion originating at point of impact
                {
                    Vector3 relVel = TargetVelocity != Vector3.zero ? vessel.Velocity() - TargetVelocity : vessel.Velocity() - BDKrakensbane.FrameVelocityV3f;
                    Ray ray = new(transform.position, relVel);
                    if (Physics.Raycast(ray, out RaycastHit hit, 500f, (int)(LayerMasks.Parts | LayerMasks.EVA | LayerMasks.Wheels)))
                    {
                        ExplosionFx.CreateExplosion(hit.point, 0.5f * (1000f * part.mass) * relVel.sqrMagnitude / 4184000f, explModelPath, explSoundPath, ExplosionSourceType.Missile, 1000f * vessel.GetRadius(), part, SourceVesselName, Team.Name, GetShortName(), ray.direction, -1, false, part.mass, -1, 1, ExplosionFx.WarheadTypes.Kinetic, null, 1.2f, sourceVelocity: vessel.Velocity());
                    }
                }
                if (part != null && !FuseFailed)
                {
                    DestroyMissile(); //splitting this off to a separate function so the clustermissile MultimissileLaunch can call it when the MML launch ienumerator is done
                }
            }

            using (var e = gaplessEmitters.GetEnumerator())
                while (e.MoveNext())
                {
                    if (e.Current == null) continue;
                    e.Current.gameObject.AddComponent<BDAParticleSelfDestruct>();
                    e.Current.transform.parent = null;
                }
            using (IEnumerator<Light> light = gameObject.GetComponentsInChildren<Light>().AsEnumerable().GetEnumerator())
                while (light.MoveNext())
                {
                    if (light.Current == null) continue;
                    light.Current.intensity = 0;
                }
        }

        public void DestroyMissile()
        {
            part.Destroy();
            part.explode();
        }

        public override Vector3 GetForwardTransform()
        {
            if (multiLauncher && multiLauncher.overrideReferenceTransform)
                return vessel.ReferenceTransform.up;
            else
                return MissileReferenceTransform.forward;
        }

        public override float GetKinematicTime()
        {
            // Get time at which the missile is traveling at the GetKinematicSpeed() speed
            if (!launched) return -1f;

            float missileKinematicTime = boostTime + cruiseTime + cruiseDelay + dropTime - TimeIndex;
            if (!vessel.InVacuum())
            {
                float speed = currentThrust > 0 ? optimumAirspeed : (float)vessel.srfSpeed;
                float minSpeed = GetKinematicSpeed();
                if (speed > minSpeed)
                {
                    float airDensity = (float)vessel.atmDensity;
                    float dragTerm;
                    float t;
                    if (useSimpleDrag)
                    {
                        dragTerm = (deployed ? deployedDrag : simpleDrag) * (0.008f * part.mass) * 0.5f * airDensity;
                        t = part.mass / (minSpeed * dragTerm) - part.mass / (speed * dragTerm);
                    }
                    else
                    {
                        float AoA = smoothedAoA.Value;
                        FloatCurve dragCurve = MissileGuidance.DefaultDragCurve;
                        float dragCd = dragCurve.Evaluate(AoA);
                        float dragMultiplier = BDArmorySettings.GLOBAL_DRAG_MULTIPLIER;
                        dragTerm = 0.5f * airDensity * currDragArea * dragMultiplier * dragCd;
                        float dragTermMinSpeed = 0.5f * airDensity * currDragArea * dragMultiplier * dragCurve.Evaluate(Mathf.Min(30f, maxAoA)); // Max AoA or 29 deg (at kink in drag curve)
                        t = part.mass / (minSpeed * dragTermMinSpeed) - part.mass / (speed * dragTerm);
                    }
                    missileKinematicTime += t; // Add time for missile to slow down to min speed
                }
            }

            return missileKinematicTime;
        }

        public override float GetKinematicSpeed()
        {
            if (vessel.InVacuum() || weaponClass != WeaponClasses.Missile) return 0f;

            // Get speed at which the missile is only capable of pulling a 2G turn at maxAoA
            float Gs = 2f;

            FloatCurve liftCurve = MissileGuidance.DefaultLiftCurve;
            float bodyGravity = (float)PhysicsGlobals.GravitationalAcceleration * (float)vessel.orbit.referenceBody.GeeASL;
            float liftMultiplier = BDArmorySettings.GLOBAL_LIFT_MULTIPLIER;
            float kinematicSpeed = BDAMath.Sqrt((Gs * part.mass * bodyGravity) / (0.5f * (float)vessel.atmDensity * currLiftArea * liftMultiplier * liftCurve.Evaluate(maxAoA)));

            return Mathf.Min(kinematicSpeed, 0.5f * (float)vessel.speedOfSound);
        }

        protected override void PartDie(Part p)
        {
            if (p != part) return;
            HasDied = true;
            Detonate();
            BDATargetManager.FiredMissiles.Remove(this);
            GameEvents.onPartDie.Remove(PartDie);
            Destroy(this); // If this is the active vessel, then KSP doesn't destroy it until we switch away, but we want to get rid of the MissileBase straight away.
        }

        public static bool CheckIfMissile(Part p)
        {
            return p.GetComponent<MissileLauncher>();
        }

        void WarnTarget()
        {
            if (targetVessel == null) return;
            var wpm = VesselModuleRegistry.GetMissileFire(targetVessel.Vessel, true);
            if (wpm != null) wpm.MissileWarning(Vector3.Distance(vessel.CoM, targetVessel.position), this);
        }

        void SetupRCS()
        {
            rcsFiredTimes = [0, 0, 0, 0];
            rcsTransforms = [upRCS, leftRCS, rightRCS, downRCS];
        }

        void DoRCS()
        {
            try
            {
                if (vacuumClearanceState == VacuumClearanceStates.Clearing || (TimeIndex < dropTime + Mathf.Min(0.5f, BDAMath.SolveTime(10f, currentThrust / part.mass)))) return; // Don't use RCS immediately after launch or when clearing a vessel to avoid running into VLS/SourceVessel
                Vector3 relV;
                if (vacuumClearanceState == VacuumClearanceStates.Turning && SourceVessel) // Clear away from launching vessel
                {
                    Vector3 relP = (vessel.CoM - SourceVessel.CoM).normalized;
                    relV = relP + (vessel.Velocity() - SourceVessel.Velocity()).normalized.ProjectOnPlanePreNormalized(relP);
                    relV = 100f * relV.ProjectOnPlane(TargetPosition - vessel.CoM);
                }
                else // Kill relative velocity to target
                    relV = TargetVelocity - vessel.Velocity();

                // Adjust for gravity if no aero or in near vacuum
                if (!aero || vessel.InNearVacuum())
                {
                    Vector3 toBody = (vessel.CoM - vessel.orbit.referenceBody.position);
                    float bodyGravity = (float)vessel.orbit.referenceBody.gravParameter / toBody.sqrMagnitude;
                    relV += -bodyGravity * vessel.up;
                }

                for (int i = 0; i < 4; i++)
                {
                    //float giveThrust = Mathf.Clamp(-localRelV.z, 0, rcsThrust);
                    float giveThrust = Mathf.Clamp(Vector3.Project(relV, rcsTransforms[i].transform.forward).magnitude * -Mathf.Sign(Vector3.Dot(rcsTransforms[i].transform.forward, relV)), 0, rcsThrust);
                    part.rb.AddForce(-giveThrust * rcsTransforms[i].transform.forward);

                    if (giveThrust > rcsRVelThreshold)
                    {
                        rcsAudioMinInterval = UnityEngine.Random.Range(0.15f, 0.25f);
                        if (Time.time - rcsFiredTimes[i] > rcsAudioMinInterval)
                        {
                            if (sfAudioSource == null) SetupAudio();
                            sfAudioSource.PlayOneShot(SoundUtils.GetAudioClip("BDArmory/Sounds/popThrust"));
                            rcsTransforms[i].emit = true;
                            rcsFiredTimes[i] = Time.time;
                        }
                    }
                    else
                    {
                        rcsTransforms[i].emit = false;
                    }

                    //turn off emit
                    if (Time.time - rcsFiredTimes[i] > rcsAudioMinInterval * 0.75f)
                    {
                        rcsTransforms[i].emit = false;
                    }
                }
            }
            catch (Exception e)
            {

                Debug.LogError("[BDArmory.MissileLauncher]: DEBUG " + e.Message);
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null part?: " + (part == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG part: " + e2.Message); }
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null part.rb?: " + (part.rb == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG part.rb: " + e2.Message); }
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null vessel?: " + (vessel == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG vessel: " + e2.Message); }
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null sfAudioSource?: " + (sfAudioSource == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: sfAudioSource: " + e2.Message); }
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null rcsTransforms?: " + (rcsTransforms == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG rcsTransforms: " + e2.Message); }
                if (rcsTransforms != null)
                {
                    for (int i = 0; i < 4; ++i)
                        try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null rcsTransforms[" + i + "]?: " + (rcsTransforms[i] == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG rcsTransforms[" + i + "]: " + e2.Message); }
                }
                try { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG null rcsFiredTimes?: " + (rcsFiredTimes == null)); } catch (Exception e2) { Debug.LogWarning("[BDArmory.MissileLauncher]: DEBUG rcsFiredTimes: " + e2.Message); }
                throw; // Re-throw the exception so behaviour is unchanged so we see it.
            }
        }

        public void KillRCS()
        {
            if (upRCS) upRCS.emit = false;
            if (downRCS) downRCS.emit = false;
            if (leftRCS) leftRCS.emit = false;
            if (rightRCS) rightRCS.emit = false;
        }

        protected override void OnGUI()
        {
            base.OnGUI();
            if (HighLogic.LoadedSceneIsFlight)
            {
                try
                {
                    drawLabels();
                    if (BDArmorySettings.DEBUG_LINES && HasFired)
                    {
                        float burnTimeleft = 10 - Mathf.Min(((TimeIndex / (boostTime + cruiseTime)) * 10), 10);

                        GUIUtils.DrawLineBetweenWorldPositions(vessel.CoM + MissileReferenceTransform.forward * burnTimeleft,
                            vessel.CoM + MissileReferenceTransform.forward * 10, 2, Color.red);
                        GUIUtils.DrawLineBetweenWorldPositions(vessel.CoM, 
                            vessel.CoM + MissileReferenceTransform.forward * burnTimeleft, 2, Color.green);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BDArmory.MissileLauncher]: Exception thrown in OnGUI: " + e.Message + "\n" + e.StackTrace);
                }
            }
        }

        void AntiSpin()
        {
            part.rb.angularDrag = 0;
            part.angularDrag = 0;
            Vector3 spin = Vector3.Project(part.rb.angularVelocity, part.rb.transform.forward);// * 8 * Time.fixedDeltaTime;
            part.rb.angularVelocity -= spin;
            //rigidbody.maxAngularVelocity = 7;

            if (guidanceActive)
            {
                part.rb.angularVelocity -= 0.6f * part.rb.angularVelocity;
            }
            else
            {
                part.rb.angularVelocity -= 0.02f * part.rb.angularVelocity;
            }
        }

        void SimpleDrag()
        {
            part.dragModel = Part.DragModel.NONE;
            if (part.rb == null || part.rb.mass == 0) return;
            //float simSpeedSquared = (float)vessel.Velocity.sqrMagnitude;
            float simSpeedSquared = (part.rb.GetPointVelocity(part.transform.TransformPoint(simpleCoD)) + (Vector3)Krakensbane.GetFrameVelocity()).sqrMagnitude;
            float drag = deployed ? deployedDrag : simpleDrag;
            float dragMagnitude = (0.008f * part.rb.mass) * drag * 0.5f * simSpeedSquared * (float)FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(vessel.CoM), FlightGlobals.getExternalTemperature(vessel.CoM), FlightGlobals.currentMainBody);
            Vector3 dragForce = dragMagnitude * vessel.Velocity().normalized;
            part.rb.AddForceAtPosition(-dragForce, transform.TransformPoint(simpleCoD));

            Vector3 torqueAxis = -Vector3.Cross(vessel.Velocity(), part.transform.forward).normalized;
            float AoA = Vector3.Angle(part.transform.forward, vessel.Velocity());
            AoA /= 20;
            part.rb.AddTorque(AoA * simpleStableTorque * dragMagnitude * torqueAxis);
        }

        public void ParseAntiRadTargetTypes()
        {
            antiradTargets = OtherUtils.ParseEnumArray<RadarWarningReceiver.RWRThreatTypes>(antiradTargetTypes);
            //Debug.Log($"[BDArmory.MissileLauncher] antiradTargets: {string.Join(", ", antiradTargets)}");
        }

        void ParseModes()
        {
            homingType = homingType.ToLower();
            switch (homingType)
            {
                case "aam":
                    GuidanceMode = GuidanceModes.AAMLead;
                    break;

                case "aamlead":
                    GuidanceMode = GuidanceModes.AAMLead;
                    break;

                case "aampure":
                    GuidanceMode = GuidanceModes.AAMPure;
                    break;
                case "aamloft":
                    GuidanceMode = GuidanceModes.AAMLoft;
                    break;
                /*case "aamhybrid":
                    GuidanceMode = GuidanceModes.AAMHybrid;
                    break;*/
                case "agm":
                    GuidanceMode = GuidanceModes.AGM;
                    break;

                case "agmballistic":
                    GuidanceMode = GuidanceModes.AGMBallistic;
                    break;

                case "cruise":
                    GuidanceMode = GuidanceModes.Cruise;
                    break;

                case "weave":
                    GuidanceMode = GuidanceModes.Weave;
                    break;

                case "sts":
                    GuidanceMode = GuidanceModes.STS;
                    break;

                case "rcs":
                    GuidanceMode = GuidanceModes.Orbital;
                    break;

                case "orbital":
                    GuidanceMode = GuidanceModes.Orbital;
                    break;

                case "beamriding":
                    GuidanceMode = GuidanceModes.BeamRiding;
                    break;

                case "slw":
                    GuidanceMode = GuidanceModes.SLW;
                    break;

                case "pronav":
                    GuidanceMode = GuidanceModes.PN;
                    break;

                case "augpronav":
                    GuidanceMode = GuidanceModes.APN;
                    break;

                case "kappa":
                    GuidanceMode = GuidanceModes.Kappa;
                    break;

                default:
                    GuidanceMode = GuidanceModes.None;
                    break;
            }

            targetingType = targetingType.ToLower();
            switch (targetingType)
            {
                case "radar":
                    TargetingMode = TargetingModes.Radar;
                    break;

                case "heat":
                    TargetingMode = TargetingModes.Heat;
                    break;

                case "laser":
                    TargetingMode = TargetingModes.Laser;
                    break;

                case "gps":
                    TargetingMode = TargetingModes.Gps;
                    maxOffBoresight = 360;
                    break;

                case "antirad":
                    TargetingMode = TargetingModes.AntiRad;
                    break;

                case "inertial":
                    TargetingMode = TargetingModes.Inertial;
                    break;

                default:
                    TargetingMode = TargetingModes.None;
                    break;
            }

            terminalGuidanceType = terminalGuidanceType.ToLower();
            switch (terminalGuidanceType)
            {
                case "radar":
                    TargetingModeTerminal = TargetingModes.Radar;
                    break;

                case "heat":
                    TargetingModeTerminal = TargetingModes.Heat;
                    break;

                case "laser":
                    TargetingModeTerminal = TargetingModes.Laser;
                    break;

                case "gps":
                    TargetingModeTerminal = TargetingModes.Gps;
                    maxOffBoresight = 360;
                    break;

                case "antirad":
                    TargetingModeTerminal = TargetingModes.AntiRad;
                    break;

                case "inertial":
                    TargetingMode = TargetingModes.Inertial;
                    break;

                default:
                    TargetingModeTerminal = TargetingModes.None;
                    break;
            }

            terminalHomingType = terminalHomingType.ToLower();
            switch (terminalHomingType)
            {
                case "aam":
                    homingModeTerminal = GuidanceModes.AAMLead;
                    break;

                case "aamlead":
                    homingModeTerminal = GuidanceModes.AAMLead;
                    break;

                case "aampure":
                    homingModeTerminal = GuidanceModes.AAMPure;
                    break;
                case "aamloft":
                    homingModeTerminal = GuidanceModes.AAMLoft;
                    break;
                case "agm":
                    homingModeTerminal = GuidanceModes.AGM;
                    break;

                case "agmballistic":
                    homingModeTerminal = GuidanceModes.AGMBallistic;
                    break;

                case "cruise":
                    homingModeTerminal = GuidanceModes.Cruise;
                    break;

                case "weave":
                    homingModeTerminal = GuidanceModes.Weave;
                    break;

                case "sts":
                    homingModeTerminal = GuidanceModes.STS;
                    break;

                case "rcs":
                    homingModeTerminal = GuidanceModes.Orbital;
                    break;

                case "orbital":
                    homingModeTerminal = GuidanceModes.Orbital;
                    break;

                case "beamriding":
                    homingModeTerminal = GuidanceModes.BeamRiding;
                    break;

                case "slw":
                    homingModeTerminal = GuidanceModes.SLW;
                    break;

                case "pronav":
                    homingModeTerminal = GuidanceModes.PN;
                    break;

                case "augpronav":
                    homingModeTerminal = GuidanceModes.APN;
                    break;

                case "kappa":
                    homingModeTerminal = GuidanceModes.Kappa;
                    break;


                default:
                    homingModeTerminal = GuidanceModes.None;
                    break;
            }

            if (!terminalHoming && GuidanceMode == GuidanceModes.AAMLoft)
            {
                if (homingModeTerminal == GuidanceModes.None)
                {
                    homingModeTerminal = GuidanceModes.PN;
                    Debug.Log($"[BDArmory.MissileLauncher]: Error in configuration of {part.name}, homingType is AAMLoft but no terminal guidance mode was specified, defaulting to pro-nav.");
                }
                else if (!(homingModeTerminal == GuidanceModes.AAMLead || homingModeTerminal == GuidanceModes.AAMPure || homingModeTerminal == GuidanceModes.PN || homingModeTerminal == GuidanceModes.APN))
                {
                    terminalHoming = true;
                    Debug.LogWarning($"[BDArmory.MissileLauncher]: Error in configuration of {part.name}, homingType is AAMLoft but an unsupported terminalHomingType: {terminalHomingType} was used without setting terminalHoming = true. ");
                }
            }

            if (terminalGuidanceShouldActivate)
            {
                if (TargetingMode == TargetingModeTerminal)
                {
                    terminalGuidanceShouldActivate = false;
                    TargetingModeTerminal = TargetingModes.None;
                }

            }
            if(TargetingMode == TargetingModes.Heat || TargetingModeTerminal == TargetingModes.Heat)
            {
                IRCCMType = IRCCMType.ToLower();
                switch (IRCCMType)
                {
                    case "seeker":
                        IRCCM = IRCCMModes.Seeker;
                        break;
                    case "fov":
                        IRCCM = IRCCMModes.gateWidth;
                        break;
                    case "fs":
                        IRCCM = IRCCMModes.SG;
                        break;
                    default:
                        IRCCM = IRCCMModes.none;
                        break;
                }
            }

            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: parsing guidance and homing complete on {part.name}");
        }

        public void ParseLiftDragSteerTorque()
        {
            // Parse lift area, we have boost, cruise and deploy and cruiseDeploy
            // the latter two are deltas, once deployed these are summed to the
            // boost and cruise value
            parsedLiftArea = ParsePerfParams(liftArea, 4);
            // Same thing for drag area
            parsedDragArea = ParsePerfParams(dragArea, 4);

            // Pre-set the deploy and cruiseDeploy values to 0 if they're < 0 since
            // these will get summed directly to currLiftArea without checking
            if (parsedLiftArea[2] < 0f)
                parsedLiftArea[2] = 0f;
            if (parsedLiftArea[3] < 0f)
                parsedLiftArea[3] = 0f;
            // Same for drag, except we also set the first value equal to liftArea if it
            // is < 0, for convenience. We only modify the cruiseArea if decoupleBoosters is
            // true, and we can check the cruise entry of drag area at staging if needed
            if (parsedDragArea[0] < 0f)
            {
                if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.MissileLauncher]: OnStart missile {shortName}: setting default dragArea to liftArea {parsedLiftArea[0]}:");
                parsedDragArea[0] = parsedLiftArea[0];
            }
            if (parsedDragArea[2] < 0f)
                parsedDragArea[2] = 0f;
            if (parsedDragArea[3] < 0f)
                parsedDragArea[3] = 0f;

            // Parse steerMult, only 2 values here needed, boost and cruise
            parsedSteerMult = ParsePerfParams(steerMult, 2);

            // Parse maxTorque, for which there are 3 values, boost, cruise and post-thrust
            // to simulate thrust vectoring
            parsedMaxTorque = ParsePerfParams(maxTorque, 3);

            // Parse maxTorqueAero, for which there are 4 values, boost, cruise and 2 deltas
            parsedMaxTorqueAero = ParsePerfParams(maxTorqueAero, 4);

            // Set the curr values
            currLiftArea = parsedLiftArea[0];
            currDragArea = parsedDragArea[0];
            currSteerMult = parsedSteerMult[0];
            currMaxTorque = parsedMaxTorque[0];
            // We check currMaxTorqueAero against 0f because this gets used directly without
            // any checks in DoAero for efficiency
            currMaxTorqueAero = Mathf.Max(parsedMaxTorqueAero[0], 0f);
        }

        private static float[] ParsePerfParams(string floatString, int length)
        {
            string[] floatStrings = floatString.Split(new char[] { ',' });
            float[] floatArray = new float[length];
            // Loop either until the end of floatStrings or length, whichever comes first
            int loopLength = (floatStrings.Length < length) ? floatStrings.Length : length;
            float temp;
            for (int i = 0; i < loopLength; i++)
            {
                if (float.TryParse(floatStrings[i], out temp))
                    floatArray[i] = temp;
                else
                    floatArray[i] = -1f;
            }
            // If floatStrings is shorter than length
            if (loopLength < length)
            {
                // Then fill the rest of the array with -1f
                for (int i = loopLength; i < length; i++)
                    floatArray[i] = -1f;
            }    

            return floatArray;
        }

        private string GetBrevityCode()
        {
            //torpedo: determine subtype
            if (missileType.ToLower() == "torpedo")
            {
                if (TargetingMode == TargetingModes.Radar && activeRadarRange > 0)
                    return "Active Sonar";

                if (TargetingMode == TargetingModes.Laser || TargetingMode == TargetingModes.Gps)
                    return "Optical/wireguided";

                if (TargetingMode == TargetingModes.Heat)
                {
                    if (activeRadarRange <= 0) return "Passive Sonar";
                    else return "Heat guided";
                }

                if (TargetingMode == TargetingModes.None)
                    return "Unguided";
            }

            if (missileType.ToLower() == "bomb")
            {
                if ((TargetingMode == TargetingModes.Laser) || (TargetingMode == TargetingModes.Gps))
                    return "JDAM";

                if ((TargetingMode == TargetingModes.None))
                    return "Unguided";
            }
            if (missileType.ToLower() == "launcher")
            {
                return "Requires Ordnance";
            }
            //else: missiles:

            if (TargetingMode == TargetingModes.Radar)
            {
                //radar: determine subtype
                if (activeRadarRange <= 0)
                    return "SARH";
                if (activeRadarRange > 0 && activeRadarRange < maxStaticLaunchRange)
                    return "Mixed SARH/F&F";
                if (activeRadarRange >= maxStaticLaunchRange)
                    return "Fire&Forget";
            }

            if (TargetingMode == TargetingModes.AntiRad)
                return "Fire&Forget";

            if (TargetingMode == TargetingModes.Heat)
                return "Fire&Forget";

            if (TargetingMode == TargetingModes.Laser)
                return "SALH";

            if (TargetingMode == TargetingModes.Gps)
            {
                return TargetingModeTerminal != TargetingModes.None ? "GPS/Terminal" : "GPS";
            }
            if (TargetingMode == TargetingModes.Inertial)
            {
                return TargetingModeTerminal != TargetingModes.None ? "Inertial/Terminal" : "Inertial";
            }
            if (TargetingMode == TargetingModes.None)
            {
                return TargetingModeTerminal != TargetingModes.None ? "Unguided/Terminal" : "Unguided";
            }
            // default:
            return "Unguided";
        }

        // RMB info in editor
        public override string GetInfo()
        {
            ParseModes();
            bool hasIRCCM = IRCCM == IRCCMModes.Seeker || IRCCM == IRCCMModes.gateWidth || IRCCM == IRCCMModes.SG;
            StringBuilder output = new StringBuilder();
            output.AppendLine($"{missileType.ToUpper()} - {GetBrevityCode()}");
            if (missileType.ToLower() == "launcher") return output.ToString(); //Launcher is empty rail, doesn't have relevant missile stats to display

            output.Append(Environment.NewLine);
            output.AppendLine($"Targeting Type: {targetingType.ToLower()}");
            output.AppendLine($"Guidance Mode: {homingType.ToLower()}");
            if (terminalHoming)
            {
                output.AppendLine($"Terminal Guidance Mode: {terminalHomingType.ToLower()} @ distance: {terminalHomingRange} m");
            }
            if (missileRadarCrossSection != RadarUtils.RCS_MISSILES)
            {
                output.AppendLine($"Detectable cross section: {missileRadarCrossSection} m^2");
            }
            output.AppendLine($"Min Range: {minStaticLaunchRange} m");
            output.AppendLine($"Max Range: {maxStaticLaunchRange} m");

            if (useFuel && weaponClass == WeaponClasses.Missile)
            {
                double dV = Math.Round(GetDeltaV(), 1);
                if (dV > 0) output.AppendLine($"Total DeltaV: {dV} m/s");
            }

            if (TargetingMode == TargetingModes.Radar)
            {
                if (activeRadarRange > 0)
                {
                    output.AppendLine($"Active Radar Range: {activeRadarRange} m");
                    if (activeRadarLockTrackCurve.maxTime > 0)
                        output.AppendLine($"- Lock/Track: {activeRadarLockTrackCurve.Evaluate(activeRadarLockTrackCurve.maxTime)} m^2 @ {activeRadarLockTrackCurve.maxTime} km");
                    else
                        output.AppendLine($"- Lock/Track: {RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS} m^2 @ {activeRadarRange / 1000} km");
                    output.AppendLine($"- LOAL: {radarLOAL}");
                    if (radarLOAL) output.AppendLine($"  - Max Radar Search Time: {radarTimeout}");
                }
                output.AppendLine($"Max Offboresight: {maxOffBoresight}");
                output.AppendLine($"Locked FOV: {lockedSensorFOV}");
            }

            if (TargetingMode == TargetingModes.Heat)
            {
                output.AppendLine($"Uncaged Lock: {uncagedLock}");
                output.AppendLine($"Min Heat threshold: {heatThreshold}");
                output.AppendLine($"Max Offboresight: {maxOffBoresight}");
                output.AppendLine($"Locked FOV: {lockedSensorFOV}");
                output.AppendLine($"IRCCM: {hasIRCCM}");
            }

            if (TargetingMode == TargetingModes.Gps || TargetingMode == TargetingModes.None || TargetingMode == TargetingModes.Inertial)
            {
                output.AppendLine($"Terminal Maneuvering: {terminalGuidanceShouldActivate}");
                if (terminalGuidanceType != "")
                {
                    output.AppendLine($"Terminal Targeting: {terminalGuidanceType} @ distance: {terminalGuidanceDistance} m");

                    if (TargetingModeTerminal == TargetingModes.Radar)
                    {
                        output.AppendLine($"Active Radar Range: {activeRadarRange} m");
                        if (activeRadarLockTrackCurve.maxTime > 0)
                            output.AppendLine($"- Lock/Track: {activeRadarLockTrackCurve.Evaluate(activeRadarLockTrackCurve.maxTime)} m^2 @ {activeRadarLockTrackCurve.maxTime} km");
                        else
                            output.AppendLine($"- Lock/Track: {RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS} m^2 @ {activeRadarRange / 1000} km");
                        output.AppendLine($"- LOAL: {radarLOAL}");
                        if (radarLOAL) output.AppendLine($"  - Radar Search Time: {radarTimeout}");
                        output.AppendLine($"Max Offboresight: {maxOffBoresight}");
                        output.AppendLine($"Locked FOV: {lockedSensorFOV}");
                    }

                    if (TargetingModeTerminal == TargetingModes.Heat)
                    {
                        output.AppendLine($"Uncaged Lock: {uncagedLock}");
                        output.AppendLine($"Min Heat threshold: {heatThreshold}");
                        output.AppendLine($"Max Offboresight: {maxOffBoresight}");
                        output.AppendLine($"Locked FOV: {lockedSensorFOV}");
                    }
                }
            }

            output.AppendLine($"Warhead:");
            foreach (var partModule in part.Modules)
            {
                if (partModule == null) continue;
                switch (partModule.moduleName)
                {
                    case "MultiMissileLauncher":
                        {
                            warheadType = WarheadTypes.Launcher; //Why is this getting set here? warHeadType is already set in onStart()

                            if (((MultiMissileLauncher)partModule).isClusterMissile)
                            {
                                output.AppendLine($"Cluster Missile:");
                                output.AppendLine($"- SubMunition Count: {((MultiMissileLauncher)partModule).salvoSize} ");
                            }
                            float tntMass = ((MultiMissileLauncher)partModule).tntMass;
                            output.AppendLine($"- Blast radius: {Math.Round(BlastPhysicsUtils.CalculateBlastRange(tntMass), 2)} m");
                            output.AppendLine($"- tnt Mass: {tntMass} kg");
                            break; //shouldn't have any other module, so break
                        }
                    case "BDModuleNuke":
                        {
                            warheadType = WarheadTypes.Nuke;
                            output.AppendLine($"- Nuclear");
                            float yield = ((BDModuleNuke)partModule).yield;
                            float radius = ((BDModuleNuke)partModule).thermalRadius;
                            float EMPRadius = ((BDModuleNuke)partModule).isEMP ? BDAMath.Sqrt(yield) * 500 : -1;
                            output.AppendLine($" - Yield: {yield} kT");
                            output.AppendLine($" - Max radius: {radius} m");
                            if (EMPRadius > 0) output.AppendLine($" - EMP Blast Radius: {Math.Round(EMPRadius)} m");
                            break; //shouldn't have any other module, so break
                        }
                    case "ClusterBomb":
                        {
                            warheadType = WarheadTypes.Standard;
                            //clusterbomb = ((ClusterBomb)partModule).submunitions.Count; //Submunitions list is populated in OnStart(), which runs after getInfo()
                            output.AppendLine($"Cluster Bomb");
                            //output.AppendLine($" - Sub-Munition Count: {clusterbomb} "); //would need adding a submunitions count int to Clusterbomb, and updating relevant .cfgs accordingly
                            continue; // to grab BDExplosivepart tnt stats
                        }
                    case "BDExplosivePart":
                        {
                            warheadType = WarheadTypes.Standard; // Also, cts rod. 
                            ((BDExplosivePart)partModule).ParseWarheadType();
                            output.AppendLine($"- {((BDExplosivePart)partModule).warheadReportingName} warhead");
                            float tntMass = ((BDExplosivePart)partModule).tntMass;
                            output.AppendLine($" - Blast radius: {Math.Round(BlastPhysicsUtils.CalculateBlastRange(tntMass), 2)} m");
                            output.AppendLine($" - TNT Mass: {tntMass} kg");
                            if (((BDExplosivePart)partModule)._warheadType == ExplosionFx.WarheadTypes.ShapedCharge)
                                output.AppendLine($" - Penetration: {ProjectileUtils.CalculatePenetration(((BDExplosivePart)partModule).caliber > 0 ? ((BDExplosivePart)partModule).caliber * 0.05f : 6f * 0.05f, 5000f, ((BDExplosivePart)partModule).tntMass * 0.0555f, ((BDExplosivePart)partModule).apMod):F2} mm");
                            continue; //in case there's also an EMP module
                        }
                    case "BDCustomWarhead":
                        {
                            warheadType = WarheadTypes.Custom;
                            //warheadType = WarheadTypes.Standard; // Also, cts rod. 
                            ((BDCustomWarhead)partModule).ParseWarheadType();
                            output.AppendLine($"- {((BDCustomWarhead)partModule).warheadReportingName} warhead");
                            output.AppendLine($"- Deviation: {Mathf.Tan(Mathf.Deg2Rad * ((BDCustomWarhead)partModule).maxDeviation) * 1000 * (1.285f / 2) * 2:F2} mrad, 80% hit");

                            BulletInfo binfo = ((BDCustomWarhead)partModule)._warheadType;
                            if (binfo == null)
                            {
                                Debug.LogError("[BDArmory.ModuleWeapon]: The requested bullet type (" + ((BDCustomWarhead)partModule).warheadType + ") does not exist.");
                                output.AppendLine($"Bullet type: {((BDCustomWarhead)partModule).warheadType} - MISSING");
                                output.AppendLine("");
                                continue;
                            }
                            output.AppendLine($"- Mass: {Math.Round(binfo.bulletMass, 2)} kg");
                            output.AppendLine($"- Additional velocity: {Math.Round(binfo.bulletVelocity, 2)} m/s");
                            //output.AppendLine($"Explosive: {binfo.explosive}");
                            if (binfo.projectileCount > 1)
                            {
                                output.AppendLine($"- Cannister Warhead");
                                output.AppendLine($" - Submunition count: {binfo.projectileCount}");
                            }
                            bool sabotTemp = (((((binfo.bulletMass * 1000) / ((binfo.caliber * binfo.caliber * Mathf.PI / 400f) * 19f) + 1f) * 10f) > binfo.caliber * 4f)) ? true : false;

                            output.AppendLine($"- Estimated Penetration: {ProjectileUtils.CalculatePenetration(binfo.caliber, binfo.bulletVelocity + optimumAirspeed, binfo.bulletMass, binfo.apBulletMod, muParam1: sabotTemp ? 0.9470311374f : 0.656060636f, muParam2: sabotTemp ? 1.555757746f : 1.20190930f, muParam3: sabotTemp ? 2.753715499f : 1.77791929f, sabot: sabotTemp):F2} mm");
                            if ((binfo.tntMass > 0) && !binfo.nuclear)
                            {
                                output.AppendLine($"- Blast:");
                                output.AppendLine($" - tnt mass:  {Math.Round(binfo.tntMass, 3)} kg");
                                output.AppendLine($" - radius:  {Math.Round(BlastPhysicsUtils.CalculateBlastRange(binfo.tntMass), 2)} m");
                                if (binfo.fuzeType.ToLower() == "timed" || binfo.fuzeType.ToLower() == "proximity" || binfo.fuzeType.ToLower() == "flak")
                                {
                                    output.AppendLine($"- Air detonation: True");
                                    output.AppendLine($" - auto timing: {(binfo.fuzeType.ToLower() != "proximity")}");
                                }
                                else
                                {
                                    output.AppendLine($"- Air detonation: False");
                                }

                                if (binfo.explosive.ToLower() == "shaped")
                                    output.AppendLine($"- Shaped Charge Penetration: {ProjectileUtils.CalculatePenetration(binfo.caliber > 0 ? binfo.caliber * 0.05f : 6f, 5000f, binfo.tntMass * 0.0555f, binfo.apBulletMod):F2} mm");
                            }
                            if (binfo.nuclear)
                            {
                                output.AppendLine($"- Nuclear Warhead:");
                                output.AppendLine($" - yield:  {Math.Round(binfo.tntMass, 3)} kT");
                                if (binfo.EMP)
                                {
                                    output.AppendLine($" - generates EMP");
                                }
                            }
                            if (binfo.EMP && !binfo.nuclear)
                            {
                                output.AppendLine($"- BlueScreen:");
                                output.AppendLine($" - EMP buildup per hit:{binfo.caliber * Mathf.Clamp(binfo.bulletMass - binfo.tntMass, 0.1f, 100)}");
                            }
                            if (binfo.impulse != 0)
                            {
                                output.AppendLine($"- Concussive:");
                                output.AppendLine($" - Impulse to target:{binfo.impulse}");
                            }
                            if (binfo.massMod != 0)
                            {
                                output.AppendLine($"- Gravitic:");
                                output.AppendLine($" - weight added per hit:{binfo.massMod * 1000} kg");
                            }
                            if (binfo.incendiary)
                            {
                                output.AppendLine($"- Incendiary");
                            }
                            if (binfo.beehive)
                            {
                                output.AppendLine($"- Beehive Warhead:");
                                string[] subMunitionData = binfo.subMunitionType.Split(new char[] { ';' });
                                string projType = subMunitionData[0];
                                if (subMunitionData.Length < 2 || !int.TryParse(subMunitionData[1], out int count)) count = 1;
                                BulletInfo sinfo = BulletInfo.bullets[projType];
                                output.AppendLine($" - deploys {count}x {(string.IsNullOrEmpty(sinfo.DisplayName) ? sinfo.name : sinfo.DisplayName)}");
                            }
                            continue; //in case there's also an HE module
                        }
                    case "ModuleEMP":
                        {
                            warheadType = WarheadTypes.EMP;
                            output.AppendLine($"- Electro-Magnetic Pulse");
                            float proximity = ((ModuleEMP)partModule).proximity;
                            output.AppendLine($" - EMP Blast Radius: {proximity} m");
                            continue; //in case a BDExplosivepart is also present
                        }
                    default: continue;
                }
                // Don't break, as some missiles contain multiple warhead types (e.g., Standard + EMP).
            }
            if (warheadType == WarheadTypes.Kinetic)
            {
                if (blastPower > 0)
                {
                    warheadType = WarheadTypes.Legacy;
                    output.AppendLine($"- Legacy Missile");
                    output.AppendLine($"- Blast Power: {blastPower}");
                }
                else
                    output.AppendLine($"- Kinetic Impactor");
            }

            return output.ToString();
        }

        #region ExhaustPrefabPooling
        static Dictionary<string, ObjectPool> exhaustPrefabPool = new Dictionary<string, ObjectPool>();
        List<GameObject> exhaustPrefabs = new List<GameObject>();

        static void AttachExhaustPrefab(string prefabPath, MissileLauncher missileLauncher, Transform exhaustTransform)
        {
            if (!CreateExhaustPool(prefabPath))
            {
                Debug.LogError($"[BDArmory.MissileLauncher]: Failed to get model {prefabPath} for {missileLauncher.part.partInfo.name}. Check that the file exists!");
                return;
            }
            var exhaustPrefab = exhaustPrefabPool[prefabPath].GetPooledObject();
            exhaustPrefab.SetActive(true);
            using (var emitter = exhaustPrefab.GetComponentsInChildren<KSPParticleEmitter>().AsEnumerable().GetEnumerator())
                while (emitter.MoveNext())
                {
                    if (emitter.Current == null) continue;
                    emitter.Current.emit = false;
                }
            exhaustPrefab.transform.parent = exhaustTransform;
            exhaustPrefab.transform.localPosition = Vector3.zero;
            exhaustPrefab.transform.localRotation = Quaternion.identity;
            missileLauncher.exhaustPrefabs.Add(exhaustPrefab);
            missileLauncher.part.OnJustAboutToDie += missileLauncher.DetachExhaustPrefabs;
            missileLauncher.part.OnJustAboutToBeDestroyed += missileLauncher.DetachExhaustPrefabs;
            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher]: Exhaust prefab " + exhaustPrefab.name + " added to " + missileLauncher.shortName + " on " + (missileLauncher.vessel != null ? missileLauncher.vessel.vesselName : "unknown"));
        }

        static bool CreateExhaustPool(string prefabPath)
        {
            if (exhaustPrefabPool == null)
            { exhaustPrefabPool = new Dictionary<string, ObjectPool>(); }
            if (!exhaustPrefabPool.ContainsKey(prefabPath) || exhaustPrefabPool[prefabPath] == null || exhaustPrefabPool[prefabPath].poolObject == null)
            {
                var exhaustPrefabTemplate = GameDatabase.Instance.GetModel(prefabPath);
                if (exhaustPrefabTemplate == null) return false;
                exhaustPrefabTemplate.SetActive(false);
                exhaustPrefabPool[prefabPath] = ObjectPool.CreateObjectPool(exhaustPrefabTemplate, 1, true, true);
            }
            return true;
        }

        void DetachExhaustPrefabs()
        {
            if (part != null)
            {
                part.OnJustAboutToDie -= DetachExhaustPrefabs;
                part.OnJustAboutToBeDestroyed -= DetachExhaustPrefabs;
            }
            foreach (var exhaustPrefab in exhaustPrefabs)
            {
                if (exhaustPrefab == null) continue;
                exhaustPrefab.transform.parent = null;
                exhaustPrefab.SetActive(false);
                if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.MissileLauncher]: Exhaust prefab " + exhaustPrefab.name + " removed from " + shortName + " on " + (vessel != null ? vessel.vesselName : "unknown"));
            }
            exhaustPrefabs.Clear();
        }
        #endregion

        public double GetDeltaV()
        {
            double specificImpulse;
            double deltaV;
            double massFlowRate;

            massFlowRate = (boostTime == 0) ? 0 : boosterFuelMass / boostTime;
            specificImpulse = (massFlowRate == 0) ? 0 : thrust / (massFlowRate * 9.81);
            deltaV = specificImpulse * 9.81 * Math.Log(part.mass / (part.mass - boosterFuelMass));

            double mass = part.mass;
            massFlowRate = (cruiseTime == 0) ? 0 : cruiseFuelMass / cruiseTime;
            if (boosterFuelMass > 0) mass -= boosterFuelMass;
            if (decoupleBoosters && boosterMass > 0) mass -= boosterMass;
            specificImpulse = (massFlowRate == 0) ? 0 : cruiseThrust / (massFlowRate * 9.81);
            deltaV += (specificImpulse * 9.81 * Math.Log(mass / (mass - cruiseFuelMass)));

            return deltaV;
        }
    }
}
