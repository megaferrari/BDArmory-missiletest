using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using BDArmory.Settings;

namespace BDArmory.ModIntegration
{
	[KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
	public class ConformalDecals : MonoBehaviour
	{
		public static ConformalDecals Instance;
		public static bool hasConformalDecals = false;
		static Assembly CDAssembly = null;
		Type MCDModType = null;
		Func<object, bool> CDisAttachedFieldGetter = null;
		Action<object, bool> CDisAttachedFieldSetter = null;

		void Awake()
		{
			if (Instance is not null) Destroy(Instance);
			Instance = this;
		}

		void Start()
		{
			CheckForConformalDecals();
			if (hasConformalDecals)
			{
				if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.ModIntegration.MouseAimFlight]: MouseAimFlight mod detected.");
				GetMCDModType();
				GetMCDIsAttachedField();
			}
			else
			{
				Destroy(this); // Destroy ourselves to not take up any further CPU cycles.
			}
		}

		public static void CheckForConformalDecals()
		{
			if (hasConformalDecals) return; // Already checked and found.
			using var a = AppDomain.CurrentDomain.GetAssemblies().ToList().GetEnumerator();
			while (a.MoveNext())
			{
				if (a.Current.FullName.StartsWith("ConformalDecals"))
				{
					CDAssembly = a.Current;
					hasConformalDecals = true;
					Debug.Log($"DEBUG Found ConformalDecals {CDAssembly.FullName}");
					return;
				}
			}
		}

		public void GetMCDModType()
		{
			if (!hasConformalDecals) return;
			foreach (var t in CDAssembly.GetTypes())
			{
				if (t == null) continue;
				if (t.Name == "ModuleConformalDecal")
				{
					MCDModType = t;
					Debug.Log($"DEBUG Found ModuleConformalDecal");
					return;
				}
			}
		}
		public void GetMCDIsAttachedField()
		{
			if (MCDModType == null) return;
			try
			{
				var fieldInfo = MCDModType.GetField("_isAttached", BindingFlags.NonPublic | BindingFlags.Instance);
				CDisAttachedFieldGetter = ReflectionUtils.CreateGetter<object, bool>(fieldInfo);
				CDisAttachedFieldSetter = ReflectionUtils.CreateSetter<object, bool>(fieldInfo);
				Debug.Log($"DEBUG Found ModuleConformalDecals._isAttached and created getter/setter");
			}
			catch (Exception e)
			{
				Debug.LogError($"[BDArmory.ConformalDecals]: Failed to find ModuleConformalDecals._isAttached. Has ConformalDecals changed? {e.Message}");
			}
		}

		public object GetMCDComponent(Part p)
		{
			if (MCDModType == null) return null;
			object component = p.GetComponent(MCDModType);
			if (component != null) Debug.Log($"DEBUG Found {MCDModType} on {p.name}");
			else Debug.Log($"DEBUG {p.name} had no {MCDModType} component.");
			return component;
		}
		public bool GetMCDIsAttached(object MCDComponent)
		{
			if (MCDComponent == null) return false;
			return CDisAttachedFieldGetter(MCDComponent);
		}
		public void SetMCDIsAttached(object MCDComponent, bool value)
		{
			if (MCDComponent == null) return;
			Debug.Log($"DEBUG Setting _isAttached to {value} on {MCDComponent}");
			CDisAttachedFieldSetter(MCDComponent, value);
		}
	}
}