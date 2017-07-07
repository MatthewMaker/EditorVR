#if UNITY_EDITOR && UNITY_EDITORVR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.EditorVR.Menus;
using UnityEditor.Experimental.EditorVR.Modules;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine;
using UnityEngine.InputNew;

namespace UnityEditor.Experimental.EditorVR.Core
{
	partial class EditorVR
	{
		const float k_MainMenuAutoHideDelay = 0.25f;

		[SerializeField]
		MainMenuActivator m_MainMenuActivatorPrefab;

		[SerializeField]
		PinnedToolButton m_PinnedToolButtonPrefab;

		class Menus : Nested, IInterfaceConnector, ILateBindInterfaceMethods<Tools>
		{
			[Flags]
			internal enum MenuHideFlags
			{
				Hidden = 1 << 0,
				Overridden = 1 << 1,
				OverUI = 1 << 2,
				OverWorkspace = 1 << 3,
				HasDirectSelection = 1 << 4
			}

			const float k_MenuHideMargin = 0.8f;
			const float k_TwoHandHideDistance = 0.25f;

			readonly Dictionary<KeyValuePair<Type, Transform>, ISettingsMenuProvider> m_SettingsMenuProviders = new Dictionary<KeyValuePair<Type, Transform>, ISettingsMenuProvider>();
			readonly Dictionary<KeyValuePair<Type, Transform>, ISettingsMenuItemProvider> m_SettingsMenuItemProviders = new Dictionary<KeyValuePair<Type, Transform>, ISettingsMenuItemProvider>();
			List<Type> m_MainMenuTools;

			// Local method use only -- created here to reduce garbage collection
			readonly List<DeviceData> m_ActiveDeviceData = new List<DeviceData>();

			public Menus()
			{
				IInstantiateMenuUIMethods.instantiateMenuUI = InstantiateMenuUI;
				IIsMainMenuVisibleMethods.isMainMenuVisible = IsMainMenuVisible;
			}

			public void ConnectInterface(object obj, Transform rayOrigin = null)
			{
				var settingsMenuProvider = obj as ISettingsMenuProvider;
				if (settingsMenuProvider != null)
					m_SettingsMenuProviders[new KeyValuePair<Type, Transform>(obj.GetType(), rayOrigin)] = settingsMenuProvider;

				var settingsMenuItemProvider = obj as ISettingsMenuItemProvider;
				if (settingsMenuItemProvider != null)
					m_SettingsMenuItemProviders[new KeyValuePair<Type, Transform>(obj.GetType(), rayOrigin)] = settingsMenuItemProvider;

				var mainMenu = obj as IMainMenu;
				if (mainMenu != null)
				{
					mainMenu.menuTools = m_MainMenuTools;
					mainMenu.menuWorkspaces = WorkspaceModule.workspaceTypes;
					mainMenu.settingsMenuProviders = m_SettingsMenuProviders;
					mainMenu.settingsMenuItemProviders = m_SettingsMenuItemProviders;
				}

				var menuOrigins = obj as IUsesMenuOrigins;
				if (menuOrigins != null)
				{
					Transform mainMenuOrigin;
					var proxy = Rays.GetProxyForRayOrigin(rayOrigin);
					if (proxy != null && proxy.menuOrigins.TryGetValue(rayOrigin, out mainMenuOrigin))
					{
						menuOrigins.menuOrigin = mainMenuOrigin;
						Transform alternateMenuOrigin;
						if (proxy.alternateMenuOrigins.TryGetValue(rayOrigin, out alternateMenuOrigin))
							menuOrigins.alternateMenuOrigin = alternateMenuOrigin;
					}
				}

				var customMenuOrigins = obj as IUsesCustomMenuOrigins;
				if (customMenuOrigins != null)
				{
					customMenuOrigins.customMenuOrigin = GetCustomMainMenuOrigin;
					customMenuOrigins.customAlternateMenuOrigin = GetCustomAlternateMenuOrigin;
				}

			}

			public void DisconnectInterface(object obj, Transform rayOrigin = null)
			{
				var settingsMenuProvider = obj as ISettingsMenuProvider;
				if (settingsMenuProvider != null)
					m_SettingsMenuProviders.Remove(new KeyValuePair<Type, Transform>(obj.GetType(), rayOrigin));

				var settingsMenuItemProvider = obj as ISettingsMenuItemProvider;
				if (settingsMenuItemProvider != null)
					m_SettingsMenuItemProviders.Remove(new KeyValuePair<Type, Transform>(obj.GetType(), rayOrigin));
			}

			public void LateBindInterfaceMethods(Tools provider)
			{
				m_MainMenuTools = provider.allTools.Where(t => !Tools.IsDefaultTool(t)).ToList(); // Don't show tools that can't be selected/toggled
			}

			static void UpdateAlternateMenuForDevice(DeviceData deviceData)
			{
				var alternateMenu = deviceData.alternateMenu;
				alternateMenu.visible = deviceData.menuHideFlags[alternateMenu] == 0 && !(deviceData.currentTool is IExclusiveMode);

				// Move the activator button to an alternate position if the alternate menu will be shown
				var mainMenuActivator = deviceData.mainMenuActivator;
				if (mainMenuActivator != null)
					mainMenuActivator.activatorButtonMoveAway = alternateMenu.visible;
			}

			Transform GetCustomMainMenuOrigin(Transform rayOrigin)
			{
				Transform mainMenuOrigin = null;

				var proxy = Rays.GetProxyForRayOrigin(rayOrigin);
				if (proxy != null)
				{
					var menuOrigins = proxy.menuOrigins;
					if (menuOrigins.ContainsKey(rayOrigin))
						mainMenuOrigin = menuOrigins[rayOrigin];
				}

				return mainMenuOrigin;
			}

			Transform GetCustomAlternateMenuOrigin(Transform rayOrigin)
			{
				Transform alternateMenuOrigin = null;

				var proxy = Rays.GetProxyForRayOrigin(rayOrigin);
				if (proxy != null)
				{
					var alternateMenuOrigins = proxy.alternateMenuOrigins;
					if (alternateMenuOrigins.ContainsKey(rayOrigin))
						alternateMenuOrigin = alternateMenuOrigins[rayOrigin];
				}

				return alternateMenuOrigin;
			}

			internal void UpdateMenuVisibilities()
			{
				m_ActiveDeviceData.Clear();
				Rays.ForEachProxyDevice(deviceData =>
				{
					m_ActiveDeviceData.Add(deviceData);
				});

				var directSelection = evr.GetNestedModule<DirectSelection>();

				foreach (var deviceData in m_ActiveDeviceData)
				{
					var alternateMenu = deviceData.alternateMenu;
					var mainMenu = deviceData.mainMenu;
					var customMenu = deviceData.customMenu;
					var menuHideFlags = deviceData.menuHideFlags;

					var mainMenuVisible = mainMenu != null && (menuHideFlags[mainMenu] & MenuHideFlags.Hidden) == 0;
					var customMenuVisible = customMenu != null && (menuHideFlags[customMenu] & MenuHideFlags.Hidden) == 0 && (menuHideFlags[customMenu] & MenuHideFlags.Overridden) == 0;
					var alternateMenuVisible = alternateMenu != null && (menuHideFlags[alternateMenu] & MenuHideFlags.Hidden) == 0;

					if (alternateMenuVisible && (mainMenuVisible || customMenuVisible))
					{
						foreach (var otherDeviceData in m_ActiveDeviceData)
						{
							if (otherDeviceData == deviceData)
								continue;

							SetAlternateMenuVisibility(otherDeviceData.rayOrigin, true);
							break;
						}
					}

					if (customMenuVisible && (mainMenuVisible || alternateMenuVisible))
						menuHideFlags[customMenu] |= MenuHideFlags.Overridden;

					var hoveringWorkspace = false;
					var rayOrigin = deviceData.rayOrigin;
					var rayOriginPosition = rayOrigin.position;
					foreach (var workspace in evr.GetModule<WorkspaceModule>().workspaces)
					{
						var workspaceTransform = workspace.transform;
						var localPosition = workspaceTransform.InverseTransformPoint(rayOriginPosition);
						var localPointerPosition = workspaceTransform.InverseTransformPoint(GetPointerPositionForRayOrigin(rayOrigin));
						if (workspace.outerBounds.Contains(localPosition) || workspace.outerBounds.Contains(localPointerPosition))
							hoveringWorkspace = true;
					}

					var menus = menuHideFlags.Keys.ToList();
					foreach (var menu in menus)
					{
						if (hoveringWorkspace)
							menuHideFlags[menu] |= MenuHideFlags.OverWorkspace;
					}

					var heldObjects = directSelection.GetHeldObjects(rayOrigin);
					var hasDirectSelection = directSelection != null && heldObjects != null && heldObjects.Count > 0;
					if (hasDirectSelection)
					{
						foreach (var menu in menus)
						{
							menuHideFlags[menu] |= MenuHideFlags.HasDirectSelection;
						}

						foreach (var otherDeviceData in m_ActiveDeviceData)
						{
							if (otherDeviceData == deviceData)
								continue;

							var otherRayOrigin = otherDeviceData.rayOrigin;
							if (directSelection.IsHovering(otherRayOrigin) || directSelection.IsScaling(otherRayOrigin)
								|| Vector3.Distance(otherRayOrigin.position, rayOriginPosition) < k_TwoHandHideDistance * Viewer.GetViewerScale())
							{
								var otherHideFlags = otherDeviceData.menuHideFlags;
								var otherMenus = otherHideFlags.Keys.ToList();
								foreach (var menu in otherMenus)
								{
									otherHideFlags[menu] |= MenuHideFlags.HasDirectSelection;
								}
								break;
							}
						}
					}
				}

				// Apply state to UI visibility
				foreach (var deviceData in m_ActiveDeviceData)
				{
					var mainMenu = deviceData.mainMenu;
					var mainMenuHideFlags = deviceData.menuHideFlags[mainMenu];
					if (mainMenuHideFlags != 0)
					{
						if ((mainMenuHideFlags & MenuHideFlags.Hidden) != 0)
						{
							mainMenu.visible = false;
						}
						else if (Time.time > deviceData.menuAutoHideTimes[mainMenu] + k_MainMenuAutoHideDelay)
						{
							mainMenu.visible = false;
						}
						else
						{
							mainMenu.visible = true;
						}
					}
					else
					{
						mainMenu.visible = true;
					}

					var customMenu = deviceData.customMenu;
					if (customMenu != null)
						customMenu.visible = deviceData.menuHideFlags[customMenu] == 0;

					UpdateAlternateMenuForDevice(deviceData);
					Rays.UpdateRayForDevice(deviceData, deviceData.rayOrigin);
				}

				// Reset Temporary states
				foreach (var deviceData in m_ActiveDeviceData)
				{
					var menuHideFlags = deviceData.menuHideFlags;
					var menus = menuHideFlags.Keys.ToList();
					foreach (var menu in menus)
					{
						var hideFlags = menuHideFlags[menu];
						if ((hideFlags & ~MenuHideFlags.Hidden & ~MenuHideFlags.Overridden) == 0)
							deviceData.menuAutoHideTimes[menu] = Time.time;

						menuHideFlags[menu] = hideFlags & ~MenuHideFlags.OverUI & ~MenuHideFlags.HasDirectSelection
							& ~MenuHideFlags.OverWorkspace;
					}
				}

				evr.GetModule<DeviceInputModule>().UpdatePlayerHandleMaps();
			}

			static Vector3 GetPointerPositionForRayOrigin(Transform rayOrigin)
			{
				return rayOrigin.position + rayOrigin.forward * DirectSelection.GetPointerLength(rayOrigin);
			}

			internal static bool OnHover(MultipleRayInputModule.RaycastSource source)
			{
				var go = source.draggedObject;
				if (!go)
					go = source.hoveredObject;

				if (go == null)
					return false;

				if (go == evr.gameObject)
					return false;

				var eventData = source.eventData;
				var rayOrigin = eventData.rayOrigin;
				var deviceData = evr.m_DeviceData.FirstOrDefault(dd => dd.rayOrigin == rayOrigin);
				if (deviceData != null)
				{
					if (go.transform.IsChildOf(deviceData.rayOrigin)) // Don't let UI on this hand block the menu
						return false;

					var scaledPointerDistance = eventData.pointerCurrentRaycast.distance / Viewer.GetViewerScale();
					var menus = deviceData.menuHideFlags.Keys.ToList();
					var hideDistance = deviceData.mainMenu.hideDistance;
					if (scaledPointerDistance < hideDistance + k_MenuHideMargin)
					{
						foreach (var menu in menus)
						{
							// Only set if hidden--value is reset every frame
							deviceData.menuHideFlags[menu] |= MenuHideFlags.OverUI;
						}

						return true;
					}
				}

				return false;
			}

			internal static void UpdateAlternateMenuOnSelectionChanged(Transform rayOrigin)
			{
				if (rayOrigin == null)
					return;

				SetAlternateMenuVisibility(rayOrigin, Selection.gameObjects.Length > 0);
			}

			internal static void SetAlternateMenuVisibility(Transform rayOrigin, bool visible)
			{
				Rays.ForEachProxyDevice(deviceData =>
				{
					var menuHideFlags = deviceData.menuHideFlags;
					var alternateMenu = deviceData.alternateMenu;
					if (alternateMenu != null)
					{
						var alternateMenuFlags = menuHideFlags[alternateMenu];
						menuHideFlags[alternateMenu] = (deviceData.rayOrigin == rayOrigin) && visible ? alternateMenuFlags & ~MenuHideFlags.Hidden : alternateMenuFlags | MenuHideFlags.Hidden;

						if ((menuHideFlags[alternateMenu] & MenuHideFlags.Hidden) != 0)
						{
							var customMenu = deviceData.customMenu;

							if (customMenu != null && (menuHideFlags[deviceData.mainMenu] & MenuHideFlags.Hidden) != 0)
							{
								menuHideFlags[customMenu] &= ~MenuHideFlags.Overridden;
							}
						}
					}
				});
			}

			internal static void OnMainMenuActivatorSelected(Transform rayOrigin, Transform targetRayOrigin)
			{
				foreach (var deviceData in evr.m_DeviceData)
				{
					var mainMenu = deviceData.mainMenu;
					if (mainMenu != null)
					{
						var customMenu = deviceData.customMenu;
						var alternateMenu = deviceData.alternateMenu;
						var menuHideFlags = deviceData.menuHideFlags;
						var alternateMenuVisible = alternateMenu != null && (menuHideFlags[alternateMenu] & MenuHideFlags.Hidden) == 0;

						if (deviceData.rayOrigin == rayOrigin)
						{
							menuHideFlags[mainMenu] ^= MenuHideFlags.Hidden;
							mainMenu.targetRayOrigin = targetRayOrigin;
							mainMenu.SendVisibilityPulse();
						}
						else
						{
							menuHideFlags[mainMenu] |= MenuHideFlags.Hidden;

							var customMenuOverridden = customMenu != null && (menuHideFlags[customMenu] & MenuHideFlags.Overridden) != 0;
							// Move alternate menu if overriding custom menu
							if (customMenuOverridden && alternateMenuVisible)
							{
								foreach (var otherDeviceData in evr.m_DeviceData)
								{
									if (deviceData == otherDeviceData)
										continue;

									if (otherDeviceData.alternateMenu != null)
										SetAlternateMenuVisibility(rayOrigin, true);
								}
							}
						}

						alternateMenuVisible = alternateMenu != null && (menuHideFlags[alternateMenu] & MenuHideFlags.Hidden) == 0;
						var mainMenuVisible = (menuHideFlags[mainMenu] & MenuHideFlags.Hidden) == 0;
						if (customMenu != null && !alternateMenuVisible && !mainMenuVisible)
							menuHideFlags[customMenu] &= ~MenuHideFlags.Overridden;
					}
				}
			}

			static GameObject InstantiateMenuUI(Transform rayOrigin, IMenu prefab)
			{
				var ui = evr.GetNestedModule<UI>();
				GameObject go = null;
				Rays.ForEachProxyDevice(deviceData =>
				{
					var proxy = deviceData.proxy;
					var otherRayOrigin = deviceData.rayOrigin;
					if (proxy.rayOrigins.ContainsValue(rayOrigin) && otherRayOrigin != rayOrigin)
					{
						Transform menuOrigin;
						if (proxy.menuOrigins.TryGetValue(otherRayOrigin, out menuOrigin))
						{
							if (deviceData.customMenu == null)
							{
								go = ui.InstantiateUI(prefab.gameObject, menuOrigin, false);

								var customMenu = go.GetComponent<IMenu>();
								deviceData.customMenu = customMenu;
								deviceData.menuHideFlags[customMenu] = 0;
							}
						}
					}
				});

				return go;
			}

			internal static IMainMenu SpawnMainMenu(Type type, InputDevice device, bool visible, out ActionMapInput input)
			{
				input = null;

				if (!typeof(IMainMenu).IsAssignableFrom(type))
					return null;

				var mainMenu = (IMainMenu)ObjectUtils.AddComponent(type, evr.gameObject);
				input = evr.GetModule<DeviceInputModule>().CreateActionMapInputForObject(mainMenu, device);
				evr.m_Interfaces.ConnectInterfaces(mainMenu, device);
				mainMenu.visible = visible;

				return mainMenu;
			}

			internal static IAlternateMenu SpawnAlternateMenu(Type type, InputDevice device, out ActionMapInput input)
			{
				input = null;

				if (!typeof(IAlternateMenu).IsAssignableFrom(type))
					return null;

				var alternateMenu = (IAlternateMenu)ObjectUtils.AddComponent(type, evr.gameObject);
				input = evr.GetModule<DeviceInputModule>().CreateActionMapInputForObject(alternateMenu, device);
				evr.m_Interfaces.ConnectInterfaces(alternateMenu, device);
				alternateMenu.visible = false;

				return alternateMenu;
			}

			internal static MainMenuActivator SpawnMainMenuActivator(InputDevice device)
			{
				var mainMenuActivator = ObjectUtils.Instantiate(evr.m_MainMenuActivatorPrefab.gameObject).GetComponent<MainMenuActivator>();
				evr.m_Interfaces.ConnectInterfaces(mainMenuActivator, device);

				return mainMenuActivator;
			}

			public static PinnedToolButton SpawnPinnedToolButton(InputDevice device)
			{
				var button = ObjectUtils.Instantiate(evr.m_PinnedToolButtonPrefab.gameObject).GetComponent<PinnedToolButton>();
				evr.m_Interfaces.ConnectInterfaces(button, device);

				return button;
			}

			internal static void UpdateAlternateMenuActions()
			{
				var actionsModule = evr.GetModule<ActionsModule>();
				foreach (var deviceData in evr.m_DeviceData)
				{
					var altMenu = deviceData.alternateMenu;
					if (altMenu != null)
						altMenu.menuActions = actionsModule.menuActions;
				}
			}

			static bool IsMainMenuVisible(Transform rayOrigin)
			{
				foreach (var deviceData in evr.m_DeviceData)
				{
					if (deviceData.rayOrigin == rayOrigin)
						return (deviceData.menuHideFlags[deviceData.mainMenu] & MenuHideFlags.Hidden) == 0;
				}

				return false;
			}
		}
	}
}
#endif
