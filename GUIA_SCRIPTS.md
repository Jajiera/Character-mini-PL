# 📘 Guía Técnica de Scripts: Variables y Funcionalidades

Esta guía documenta exhaustivamente todos los scripts de la arquitectura del proyecto **Character mini PL**, detallando su propósito, variables de Inspector (`[SerializeField]`), propiedades de solo lectura, métodos clave y eventos desacoplados.

---

## 📑 Tabla de Contenidos
1. [Arquitectura General y Flujo de Datos](#arquitectura-general-y-flujo-de-datos)
2. [Subflujo Character & Locomoción](#1-subflujo-character--locomoción)
   - [PlayerCharacter.cs](#playercharactercs)
   - [GroundDetector.cs](#grounddetectorcs)
   - [CharacterVisualFeedback.cs](#charactervisualfeedbackcs)
3. [Subflujo State Machine (Máquina de Estados)](#2-subflujo-state-machine-máquina-de-estados)
   - [PlayerStateMachine.cs](#playerstatemachinecs)
   - [PlayerStateBase.cs](#playerstatebasecs)
   - [Estados de Locomoción (Idle, Walking, Sprinting, Jumping)](#estados-de-locomoción)
   - [Estados Tácticos (Crouching, Prone)](#estados-tácticos)
   - [Estados Evasivos (Sliding, Rolling)](#estados-evasivos)
4. [Subflujo Combat & Comandos en Buffer](#3-subflujo-combat--comandos-en-buffer)
   - [Weapon.cs](#weaponcs-abstracto)
   - [ProjectileWeapon.cs](#projectileweaponcs)
   - [Bullet.cs](#bulletcs)
   - [CombatCommandQueue.cs](#combatcommandqueuecs)
   - [AttackCommand.cs & BaseCombatCommand.cs](#attackcommandcs--basecombatcommandcs)
   - [InteractCommand.cs](#interactcommandcs)
5. [Subflujo Input & Cinemachine Camera](#4-subflujo-input--cinemachine-camera)
   - [InputReader.cs](#inputreadercs)
   - [ThirdPersonCameraController.cs](#thirdpersoncameracontrollercs)
6. [Subflujo Interacción con el Entorno](#5-subflujo-interacción-con-el-entorno)
   - [InteractionDetector.cs](#interactiondetectorcs)
   - [InteractiveSwitch.cs](#interactiveswitchcs)
   - [CollectibleCube.cs](#collectiblecubecs)
7. [Subflujo Datos & Perfiles (Flyweight)](#6-subflujo-datos--perfiles-flyweight)
   - [MovementDataSO.cs](#movementdatasocs)
   - [CharacterDataSO.cs](#characterdatasocs)
8. [Interfaces del Core](#7-interfaces-del-core)
9. [Scripts Auxiliares y Legado](#8-scripts-auxiliares-y-legado)

---

## Arquitectura General y Flujo de Datos

```
[InputSystem_Actions]
        │
        ▼
  [InputReader] (ScriptableObject)
        │
        ├─ Eventos C# ──► [PlayerCharacter] ◄──► [PlayerStateMachine] ◄──► [IState Instances]
        │                        │
        │                        ├─► [GroundDetector] (SphereCast No-Alloc)
        │                        ├─► [CombatCommandQueue] ──► [AttackCommand] ──► [Weapon / ProjectileWeapon]
        │                        ├─► [InteractionDetector] ──► [IInteractable (Switch, Cube)]
        │                        └─► [CharacterVisualFeedback] (MaterialPropertyBlock + HUD OnGUI)
        │
        └─ Eventos Zoom/Aim ──► [ThirdPersonCameraController] ──► [CinemachineCamera + AimCamera]
```

---

## 1. Subflujo Character & Locomoción

### `PlayerCharacter.cs`
- **Ubicación:** `Assets/Scripts/Character/PlayerCharacter.cs`
- **Namespace:** `Scripts.Character`
- **Interfaces Implementadas:** `IDamageable`
- **Dependencias Requeridas:** `CharacterController`, `PlayerStateMachine`, `GroundDetector`, `CombatCommandQueue`, `InteractionDetector`
- **Propósito:** Hub central del personaje. Orquesta la máquina de estados, el cambio dinámico de dimensiones de cápsula (de pie, agachado, cuerpo a tierra), la alineación con la cámara/mira, la carga progresiva del ataque y el cálculo de movimiento físico.

#### Variables Serializadas (`[SerializeField]`):
| Variable | Tipo | Default | Descripción |
| :--- | :--- | :--- | :--- |
| `characterProfile` | `CharacterDataSO` | `null` | Perfil del personaje (ScriptableObject con salud, stamina, datos de movimiento). |
| `fallbackMovementData` | `MovementDataSO` | `null` | Datos de movimiento de respaldo si no hay perfil asignado. |
| `inputReader` | `InputReader` | `null` | Canal de entrada desacoplado de Unity Input System. |
| `groundDetector` | `GroundDetector` | `null` | Referencia al detector de suelo (se auto-detecta en Awake si es null). |
| `stateMachine` | `PlayerStateMachine` | `null` | Gestor de estados de locomoción y táctica. |
| `commandQueue` | `CombatCommandQueue` | `null` | Cola FIFO con buffer de tiempo para comandos de combate e interacción. |
| `interactionDetector` | `InteractionDetector`| `null` | Componente de escaneo radial para objetos interactuables. |
| `characterController` | `CharacterController`| `null` | Componente nativo de físicas cinemáticas de Unity. |
| `visualModel` | `Transform` | `null` | Modelo visual 3D que escala verticalmente y horizontalmente según la postura. |
| `stanceTransitionSpeed`| `float` | `12.0f` | Velocidad de interpolación (`Mathf.Lerp`) al cambiar de postura (de pie, agachado, prone). |
| `shouldFaceMoveDirection`| `bool` | `false` | Si es `false`, el personaje strafea dando la espalda a la cámara. Si es `true`, rota hacia la dirección de movimiento. |
| `cameraTransform` | `Transform` | `null` | Transform de la cámara principal utilizado para orientar el movimiento relativo. |
| `eyeTarget` | `Transform` | `null` | Punto de mira para pitch vertical al apuntar (IK / LookAt target). |
| `crosshairUI` | `GameObject` | `null` | Referencia a la mirilla (`miraDisparo`). Se auto-detecta si está vacía. |
| `aimSensitivityX` | `float` | `0.15f` | Sensibilidad de giro horizontal del cuerpo (Yaw) al apuntar. |
| `aimSensitivityY` | `float` | `0.15f` | Sensibilidad de inclinación vertical del EyeTarget (Pitch) al apuntar. |
| `aimPitchMin` | `float` | `-45.0f` | Límite mínimo de ángulo vertical de apuntado (mirar arriba). |
| `aimPitchMax` | `float` | `60.0f` | Límite máximo de ángulo vertical de apuntado (mirar abajo). |
| `currentWeapon` | `Weapon` | `null` | Arma equipada (ej. `ProjectileWeapon`). Se auto-detecta en los hijos si es null. |
| `maxAttackChargeTime` | `float` | `1.5f` | Tiempo en segundos necesario para alcanzar el 100% de carga del ataque. |
| `baseAttackDamage` | `float` | `15.0f` | Daño mínimo al soltar el ataque instantáneamente (0% carga). |
| `maxAttackDamage` | `float` | `45.0f` | Daño máximo al soltar el ataque tras completar la carga (100% carga). |

#### Propiedades Públicas:
- `ActiveMovementData`: Devuelve los datos de movimiento activos del perfil o fallback.
- `CommandQueue`: Acceso a la cola de comandos de combate.
- `GroundDetector`: Acceso al detector de suelo.
- `InteractionDetector`: Acceso al detector de interacción.
- `IsInvulnerable`: Estado de invulnerabilidad (activo durante el `RollingState`).
- `VerticalVelocity`: Velocidad vertical actual acumulada por la gravedad o salto.
- `IsAiming`: Retorna `true` si el jugador mantiene pulsado el botón derecho (Aim).
- `EyeTarget`: Transform del punto focal de la mirada.
- `IsChargingAttack`: Retorna `true` si el botón de ataque está presionado y cargando.
- `AttackChargeRatio`: Valor normalizado entre `0.0f` y `1.0f` con el progreso de carga actual.
- Instancias de estados: `IdleState`, `WalkingState`, `SprintingState`, `JumpingState`, `CrouchingState`, `ProneState`, `SlidingState`, `RollingState`.

#### Métodos Públicos:
- `AccelerateTowards(Vector3 targetDirection, float targetSpeed, float rate)`: Acelera la velocidad horizontal actual hacia la dirección y velocidad deseadas usando `Vector3.MoveTowards`.
- `Decelerate(float rate)`: Frena progresivamente la velocidad horizontal hacia cero.
- `SetVerticalVelocity(float velocity)`: Sobrescribe la velocidad vertical (utilizado por el salto).
- `SetHorizontalVelocity(Vector3 velocity)`: Asigna directamente la velocidad horizontal (utilizado por deslizamiento y rodillo).
- `ResetHorizontalVelocity()`: Pone la velocidad horizontal a `Vector3.zero`.
- `CalculateWorldMovementDirection(Vector2 moveInput)`: Convierte el input 2D del teclado/mando a un vector 3D normalizado relativo al ángulo horizontal de la cámara.
- `RotateTowards(Vector3 targetDirection, float smoothTime)`: Rota el personaje suavemente usando `Mathf.SmoothDampAngle` (ignorado si está apuntando o strafeando).
- `IsGrounded()`: Valida si el personaje toca suelo estable mediante `CharacterController.isGrounded` o `GroundDetector.IsGroundedAndStable()`.
- `ApplyGravity()`: Aplica la aceleración de la gravedad con fuerza constante de anclaje de `-2m/s` en pendientes.
- `MoveWithCurrentVelocity()`: Desplaza el `CharacterController` con el vector resultante combinado (`currentVelocity + verticalVelocity`).
- `SetStanceDimensions(float targetHeight, Vector3 targetCenter)`: Modifica la altura objetivo de la cápsula calculando matemáticamente el anclaje a la base de los pies para evitar que el personaje flote.
- `SetInvulnerability(bool active)`: Activa o desactiva la inmunidad a daño.
- `TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)`: Implementación de `IDamageable`. Resta vida si no es invulnerable.
- `IsAlive()`: Retorna `currentHealth > 0`.

#### Eventos de Observador:
- `AttackTriggeredEvent`: Disparado al iniciar la liberación de un ataque.
- `AttackReleasedEvent(float chargeRatio)`: Disparado al soltar el botón de ataque con el ratio alcanzado (0 a 1).
- `AttackChargeStartedEvent`: Disparado al comenzar a mantener pulsado el botón de ataque.
- `AttackMaxChargeReachedEvent`: Disparado cuando el temporizador de carga llega al 100%.
- `InteractTriggeredEvent`: Disparado al ejecutar una interacción.

---

### `GroundDetector.cs`
- **Ubicación:** `Assets/Scripts/Character/GroundDetector.cs`
- **Namespace:** `Scripts.Character`
- **Propósito:** Detección de suelo de alta precisión mediante `Physics.OverlapSphereNonAlloc`, evitando asignaciones de memoria en el Garbage Collector (GC) y filtrando colisionadores propios.

#### Variables Serializadas (`[SerializeField]`):
| Variable | Tipo | Default | Descripción |
| :--- | :--- | :--- | :--- |
| `groundCheckPoint` | `Transform` | `null` | Punto inferior de chequeo (hijo `GroundCheck`). Si es null usa offset relativo. |
| `movementParameters`| `MovementDataSO` | `null` | Configuración de radio de esfera y máscaras de capas. |
| `characterController`| `CharacterController`| `null` | Referencia al controlador cinemático. |

#### Métodos Públicos:
- `IsGroundedAndStable()`: Retorna `true` si el `CharacterController` detecta suelo o si el `OverlapSphereNonAlloc` intersecta colliders válidos ignorando triggers y colisionadores propios.
- `TryGetGroundHit(out RaycastHit hit)`: Lanza un raycast hacia abajo para obtener el punto de impacto y la normal del suelo.
- `SetMovementData(MovementDataSO data)`: Actualiza en caliente los parámetros de chequeo de suelo.

---

### `CharacterVisualFeedback.cs`
- **Ubicación:** `Assets/Scripts/Character/CharacterVisualFeedback.cs`
- **Namespace:** `Scripts.Character`
- **Propósito:** Cumplir el Principio de Responsabilidad Única (SRP) en la presentación visual. Modifica los colores del material mediante `MaterialPropertyBlock` (sin clonar materiales en memoria) y dibuja el HUD de la barra de carga de ataque con `OnGUI`.

#### Variables Serializadas (`[SerializeField]`):
| Variable | Tipo | Default | Descripción |
| :--- | :--- | :--- | :--- |
| `playerCharacter` | `PlayerCharacter` | `null` | Referencia al personaje observado. |
| `stateMachine` | `PlayerStateMachine` | `null` | Referencia a la máquina de estados. |
| `targetRenderer` | `Renderer` | `null` | Renderer del modelo 3D donde se aplica el color. |
| `idleColor` | `Color` | `Blanco` | Color en estado Idle. |
| `walkingColor` | `Color` | `Azul Cielo` | Color en estado Walking. |
| `sprintingColor` | `Color` | `Azul Intenso`| Color en estado Sprinting. |
| `jumpingColor` | `Color` | `Verde Turquesa`| Color en estado Jumping. |
| `crouchingColor` | `Color` | `Amarillo Ámbar`| Color en estado Crouching. |
| `proneColor` | `Color` | `Naranja Oscuro`| Color en estado Prone. |
| `slidingColor` | `Color` | `Púrpura` | Color en estado Sliding. |
| `rollingColor` | `Color` | `Amarillo Brillante`| Color en estado Rolling. |
| `attackFlashColor` | `Color` | `Rojo` | Destello al disparar o liberar ataque. |
| `interactFlashColor` | `Color` | `Verde` | Destello al interactuar con objetos. |
| `flashDuration` | `float` | `0.2f` | Duración del destello de acción en segundos. |
| `colorTransitionSpeed`| `float` | `10.0f` | Velocidad de suavizado del cambio de color entre estados. |
| `chargeStartColor` | `Color` | `Ámbar Dorado` | Color inicial al comenzar a cargar ataque. |
| `chargeMaxColor` | `Color` | `Rojo Fuego` | Color al alcanzar el 100% de carga. |
| `chargePulseColor` | `Color` | `Oro Brillante`| Color del pulso de advertencia al estar cargado al máximo. |
| `maxChargePulseSpeed` | `float` | `14.0f` | Frecuencia del efecto de pulsación luminosa al 100% de carga. |
| `showChargeHUD` | `bool` | `true` | Habilita o deshabilita la barra visual de carga en pantalla (`OnGUI`). |

---

## 2. Subflujo State Machine (Máquina de Estados)

### `PlayerStateMachine.cs`
- **Ubicación:** `Assets/Scripts/StateMachine/PlayerStateMachine.cs`
- **Namespace:** `Scripts.StateMachine`
- **Propósito:** Gestor formal del patrón State. Maneja el ciclo de vida de los estados (`Enter`, `Execute`, `FixedExecute`, `Exit`) y notifica a observadores mediante eventos.

#### Propiedades Públicas:
- `CurrentState`: Instancia actual del estado que implementa `IState`.

#### Métodos Públicos:
- `Initialize(IState startingState)`: Asigna el estado inicial y ejecuta su `Enter()`.
- `ChangeState(IState newState)`: Ejecuta `Exit()` del estado anterior, actualiza la referencia, ejecuta `Enter()` del nuevo y dispara `StateChangedEvent`.
- `Tick()`: Ejecuta `CurrentState.Execute()` cada fotograma (`Update`).
- `FixedTick()`: Ejecuta `CurrentState.FixedExecute()` en el ciclo de físicas (`FixedUpdate`).

#### Eventos:
- `StateChangedEvent(IState newState)`: Notifica el cambio de estado a componentes visuales y auditivos.

---

### `PlayerStateBase.cs`
- **Ubicación:** `Assets/Scripts/StateMachine/PlayerStateBase.cs`
- **Namespace:** `Scripts.StateMachine`
- **Propósito:** Clase base abstracta para todos los estados del jugador. Almacena las referencias protegidas `character`, `stateMachine`, `inputReader` y `MovementData`.

---

### Estados de Locomoción

#### `IdleState.cs`
- **Enter:** Detiene el movimiento horizontal (`ResetHorizontalVelocity()`), ajusta dimensiones de cápsula a postura de pie (`StandingHeight`, `StandingCenter`).
- **Execute:** Transiciona a `WalkingState` si `CurrentMoveInput.sqrMagnitude > 0.01f`. Si pierde el suelo, transiciona a `JumpingState`.
- **FixedExecute:** Aplica gravedad y mueve el CharacterController.

#### `WalkingState.cs`
- **Enter:** Ajusta postura a dimensiones de pie.
- **Execute:** Si no hay input, transiciona a `IdleState`. Si el sprint está activo y no está apuntando, transiciona a `SprintingState`. Si pierde suelo, a `JumpingState`.
- **FixedExecute:** Calcula dirección relativa a cámara, acelera hacia `WalkSpeed`, rota y aplica físicas.

#### `SprintingState.cs`
- **Enter:** Dimensiones de pie.
- **Execute:** Si no hay input, regresa a `IdleState`. Si el usuario desactiva sprint o empieza a apuntar (`IsAiming`), regresa a `WalkingState`. Si presiona agacharse (`C`), pasa a `SlidingState`.
- **FixedExecute:** Acelera hacia `SprintSpeed`, rota y aplica gravedad.

#### `JumpingState.cs`
- **Enter:** Aplica impulso ascendente `SetVerticalVelocity(MovementData.JumpForce)`.
- **Execute:** Detecta el aterrizaje cuando la velocidad vertical desciende y `IsGrounded()` es true, regresando a `WalkingState` o `IdleState`.
- **FixedExecute:** Aplica control aéreo (`AirControl`), gravedad acumulada y desplazamiento.

---

### Estados Tácticos

#### `CrouchingState.cs`
- **Enter:** Ajusta las dimensiones de cápsula a `CrouchingHeight` y `CrouchingCenter`.
- **Execute:** Al pulsar `C` pasa a `ProneState`. Si se activa sprint o salto, sale de agachado.
- **FixedExecute:** Acelera hacia `CrouchSpeed`, rota hacia la dirección de avance y aplica físicas.

#### `ProneState.cs`
- **Enter:** Reduce la altura de la cápsula a `ProneHeight` y `ProneCenter` (cuerpo a tierra).
- **Execute:** Al pulsar `C` o saltar, se levanta a `WalkingState` o `IdleState`.
- **FixedExecute:** Desplazamiento lento a `ProneSpeed` con anclaje a ras de suelo.

---

### Estados Evasivos

#### `SlidingState.cs`
- **Enter:** Reduce altura a agachado, proyecta la inercia del sprint con velocidad inicial `SlideInitialSpeed`.
- **Execute:** Temporizador de deslizamiento (`SlideDuration`). Al terminar, transiciona a `CrouchingState` o `WalkingState`.
- **FixedExecute:** Desacelera progresivamente la inercia hasta que expira el tiempo.

#### `RollingState.cs`
- **Enter:** Activa invulnerabilidad con `character.SetInvulnerability(true)`, reduce altura de cápsula y asigna velocidad de rodamiento `RollSpeed` en la dirección actual.
- **Execute:** Temporizador de rodamiento (`RollDuration`). Al finalizar el tiempo, pasa a `IdleState` o `WalkingState`.
- **FixedExecute:** Desplaza al personaje con el vector de rodillo ininterrumpido.
- **Exit:** Desactiva invulnerabilidad con `character.SetInvulnerability(false)`.

---

## 3. Subflujo Combat & Comandos en Buffer

### `Weapon.cs` (Abstracto)
- **Ubicación:** `Assets/Scripts/Combat/Weapon.cs`
- **Namespace:** `Scripts.Combat`
- **Propósito:** Contrato base extensible para cualquier arma del juego (armas de proyectil, hitscan, armas cuerpo a cuerpo).

#### Variables Protegidas (`[SerializeField]`):
- `weaponName` (`string`): Nombre identificador del arma.
- `firePoint` (`Transform`): Punto exacto en la punta del cañón desde donde nace el proyectil.
- `baseDamage` (`float`): Daño base del arma sin carga.
- `maxDamage` (`float`): Daño máximo del arma con carga completa.
- `fireRate` (`float`): Cadencia de disparo (segundos mínimos entre disparos).

#### Métodos Abstractos:
- `bool CanFire()`: Retorna si la cadencia de fuego permite disparar.
- `void Fire(float chargeRatio = 0f)`: Ejecuta la lógica de disparo modulada por el nivel de carga (0.0 a 1.0).

---

### `ProjectileWeapon.cs`
- **Ubicación:** `Assets/Scripts/Combat/ProjectileWeapon.cs`
- **Namespace:** `Scripts.Combat`
- **Hereda de:** `Weapon`
- **Propósito:** Arma física balística. Calcula el punto de mira exacto proyectando un raycast desde la cámara hacia la retícula central y reorienta el proyectil para que converja en dicho punto.

#### Variables Serializadas (`[SerializeField]`):
| Variable | Tipo | Default | Descripción |
| :--- | :--- | :--- | :--- |
| `bulletPrefab` | `GameObject` | `null` | Prefab de la bala con componente `Bullet`. Si es null, genera una bala por código. |
| `shootForce` | `float` | `60.0f` | Velocidad e impulso de eyección del proyectil. |
| `hitLayers` | `LayerMask` | `~0` (Todo)| Capas que bloquean la línea de visión del raycast de apuntado. |
| `maxRaycastDistance` | `float` | `150.0f` | Distancia máxima para trazar el punto de mira desde la cámara. |
| `aimCamera` | `Transform` | `null` | Transform de la cámara de apuntado. Se auto-detecta si es null. |
| `muzzleFlash` | `ParticleSystem`| `null`| Partículas del fogonazo del cañón al disparar. |
| `shootSound` | `AudioClip` | `null` | Clip de audio del disparo. |
| `audioSource` | `AudioSource` | `null` | Componente de audio 3D/espacial. |
| `addTracerTrail` | `bool` | `true` | Agrega estela de luz (TrailRenderer) si la bala no la tiene. |
| `modelTransform` | `Transform` | `null` | Transform del modelo visual para la animación procedimental de retroceso. |
| `recoilKickBack` | `float` | `0.06f` | Distancia hacia atrás que retrocede el arma al disparar. |
| `recoilKickUp` | `float` | `4.0f` | Ángulo de elevación (rotación en X) por el retroceso. |
| `recoilReturnSpeed`| `float` | `10.0f` | Velocidad de recuperación hacia la posición de reposo del arma. |
| `standaloneInput` | `bool` | `false` | Si es true, escucha el input por su cuenta sin requerir PlayerCharacter. |
| `isSemiAutomatic` | `bool` | `true` | Disparo tiro a tiro vs continuo. |

#### Métodos Públicos:
- `Fire(float chargeRatio = 0f)`: Instancia el proyectil, calcula daño dinámico con `Mathf.Lerp(baseDamage, maxDamage, chargeRatio)`, orienta la trayectoria hacia el crosshair y aplica retroceso procedimental.

---

### `Bullet.cs`
- **Ubicación:** `Assets/Scripts/Combat/Bullet.cs`
- **Namespace:** `Scripts.Combat`
- **Propósito:** Comportamiento balístico del proyectil, detección de impactos por trigger y colisión, aplicación de daño a objetivos `IDamageable`, transferencia de impulso a `Rigidbody` y partículas de impacto.

#### Variables Serializadas (`[SerializeField]`):
| Variable | Tipo | Default | Descripción |
| :--- | :--- | :--- | :--- |
| `damage` | `float` | `20.0f` | Daño aplicado al impactar. |
| `speed` | `float` | `60.0f` | Velocidad de avance de la bala. |
| `lifeTime` | `float` | `5.0f` | Tiempo de autodestrucción si no colisiona contra nada. |
| `impactForce` | `float` | `8.0f` | Fuerza de impacto físico (`ForceMode.Impulse`) sobre objetos con Rigidbody. |
| `impactParticles`| `GameObject` | `null` | Prefab de chispas/explosión al impactar. |

#### Métodos Públicos:
- `Initialize(float bulletDamage, float bulletSpeed)`: Configura el daño y la velocidad de la bala al momento de ser instanciada por el arma.

---

### `CombatCommandQueue.cs`
- **Ubicación:** `Assets/Scripts/Combat/CombatCommandQueue.cs`
- **Namespace:** `Scripts.Combat`
- **Propósito:** Cola FIFO de comandos de combate que implementa Command Pattern con soporte para expiración por tiempo (Input Buffer).

#### Variables Serializadas (`[SerializeField]`):
- `maxQueueSize` (`int`, default: `3`): Límite máximo de comandos en la cola (descarta el más antiguo al saturarse).

#### Métodos Públicos:
- `EnqueueCommand(ICommand command)`: Agrega un nuevo comando a la cola.
- `TryExecuteNextCommand()`: Extrae y evalúa el siguiente comando válido; si expiró lo descarta y evalúa el siguiente hasta ejecutar uno o vaciar la cola.
- `ClearQueue()`: Purga todos los comandos almacenados.

---

### `AttackCommand.cs` & `BaseCombatCommand.cs`
- **Ubicación:** `Assets/Scripts/Combat/AttackCommand.cs` y `BaseCombatCommand.cs`
- **Namespace:** `Scripts.Combat`
- **Propósito:** Comando que encapsula la intención de disparar/atacar. Almacena la ratio de carga acumulada, calcula el daño final escalado y dispara el arma actual del jugador.
- **`BaseCombatCommand`:** Controla el ciclo de vida del comando en base al tiempo de buffer (`lifeTime`). Si transcurre más tiempo del permitido antes de que el personaje pueda ejecutarlo, `IsExpired()` devuelve `true`.

---

### `InteractCommand.cs`
- **Ubicación:** `Assets/Scripts/Combat/InteractCommand.cs`
- **Namespace:** `Scripts.Combat`
- **Propósito:** Comando encolable para solicitar interacción con objetos del entorno.

---

## 4. Subflujo Input & Cinemachine Camera

### `InputReader.cs`
- **Ubicación:** `Assets/Scripts/Input/InputReader.cs`
- **Namespace:** `Scripts.Input`
- **Tipo:** `ScriptableObject` (implementa `InputSystem_Actions.IPlayerActions`)
- **Propósito:** Puente abstracto desacoplado entre el New Input System de Unity y la lógica de juego. Expone propiedades y eventos C# puros a los que cualquier script puede suscribirse.

#### Propiedades Públicas:
- `CurrentMoveInput` (`Vector2`): Vector normalizado de movimiento (WASD / Stick izquierdo).
- `CurrentLookInput` (`Vector2`): Delta del ratón o Stick derecho.
- `CurrentZoomDelta` (`Vector2`): Delta de la rueda del ratón o gatillos de zoom.
- `IsAttackPressed` (`bool`): Indica si el botón de ataque está presionado actualmente.
- `IsAiming` (`bool`): Indica si el modo de apuntado está activo (botón derecho / gatillo izquierdo).
- `IsSprintActive` (`bool`): Estado toggle del sprint.

#### Eventos Públicos:
- `MoveEvent(Vector2)`: Notifica cambios en el vector de movimiento.
- `LookEvent(Vector2)`: Notifica movimiento de la mirada.
- `JumpStartedEvent` / `JumpCanceledEvent`: Pulsación y liberación de Salto (Espacio).
- `SprintStartedEvent` / `SprintCanceledEvent`: Activación y desactivación de Sprint (Shift).
- `CrouchPerformedEvent`: Pulsación de Agacharse/Prone (Tecla C).
- `RollPerformedEvent`: Pulsación de Rodillo/Evasión (Tecla Alt / Q).
- `AttackStartedEvent` / `AttackPerformedEvent` / `AttackCanceledEvent`: Eventos de inicio, mantenimiento y liberación de Ataque (Click izquierdo).
- `InteractPerformedEvent`: Pulsación de Interacción (Tecla E).
- `AimEvent(bool)`: Cambio en el estado de apuntado.
- `ZoomDeltaEvent(Vector2)`: Entrada de rueda de ratón para zoom de cámara.

#### Métodos Públicos:
- `EnablePlayerInput()`: Activa el mapa de acciones del jugador.
- `DisablePlayerInput()`: Desactiva el mapa de acciones.
- `ToggleSprint()`: Alterna el estado de sprint.
- `SetSprintActive(bool active)`: Establece explícitamente el estado de sprint.
- `SetAimActive(bool active)`: Establece el estado de apuntado (si activa apuntado, cancela automáticamente el sprint).

---

### `ThirdPersonCameraController.cs`
- **Ubicación:** `Assets/Scripts/Camera/ThirdPersonCameraController.cs`
- **Namespace:** `Scripts.CameraSystem`
- **Dependencias Requeridas:** `CinemachineCamera` (Unity Cinemachine 3/6)
- **Propósito:** Control de cámara en tercera persona. Gestiona el zoom orbital suave de Cinemachine, el bloqueo de cursor, el cambio prioritario a la cámara de apuntado (`aimCamera`) y la sincronización con la mira en pantalla.

#### Variables Serializadas (`[SerializeField]`):
| Variable | Tipo | Default | Descripción |
| :--- | :--- | :--- | :--- |
| `zoomStep` | `float` | `1.0f` | Metros modificados por cada muesca de la rueda del ratón. |
| `gamepadZoomSpeed` | `float` | `5.0f` | Velocidad de zoom continuo para mando. |
| `zoomLerpSpeed` | `float` | `10.0f` | Suavizado (`Mathf.Lerp`) hacia la distancia de zoom objetivo. |
| `minDistance` | `float` | `1.5f` | Distancia mínima de aproximación de la cámara. |
| `maxDistance` | `float` | `15.0f` | Distancia máxima de alejamiento de la cámara. |
| `lockCursor` | `bool` | `true` | Bloquea y oculta el cursor del ratón en pantalla al iniciar. |
| `inputReader` | `InputReader` | `null` | Referencia al canal de entradas de zoom y apuntado. |
| `aimCamera` | `CinemachineCamera`| `null` | Cámara de hombro para apuntado (`ThirdPersonAimCamera`). |
| `normalPriority` | `int` | `10` | Prioridad de Cinemachine en modo de exploración libre. |
| `aimPriority` | `int` | `20` | Prioridad de Cinemachine al apuntar (toma el control visual). |
| `crosshairUI` | `GameObject` | `null` | Objeto o Canvas de la retícula de disparo (`miraDisparo`). |

---

## 5. Subflujo Interacción con el Entorno

### `InteractionDetector.cs`
- **Ubicación:** `Assets/Scripts/Interaction/InteractionDetector.cs`
- **Namespace:** `Scripts.Interaction`
- **Propósito:** Escanea en tiempo real objetos interactuables (`IInteractable`) dentro de un radio esférico usando `Physics.OverlapSphereNonAlloc`. Maneja el cambio de foco (`OnFocusGained` / `OnFocusLost`).

#### Variables Serializadas (`[SerializeField]`):
| Variable | Tipo | Default | Descripción |
| :--- | :--- | :--- | :--- |
| `detectionRadius` | `float` | `3.0f` | Radio en metros del área de detección de interacción. |
| `detectionOffset` | `Vector3` | `(0, 0.5, 0)` | Desplazamiento respecto al centro del jugador. |
| `interactableLayer`| `LayerMask` | `~0` (Todo)| Capas evaluadas para objetos interactuables. |
| `detectionOrigin` | `Transform` | `null` | Transform central de origen (si es null usa transform del jugador). |

#### Métodos Públicos:
- `TryInteract()`: Ejecuta inmediatamente la interacción contra el objeto interactuable enfocado más cercano y retorna `true` si tuvo éxito.

---

### `InteractiveSwitch.cs`
- **Ubicación:** `Assets/Scripts/Interaction/InteractiveSwitch.cs`
- **Namespace:** `Scripts.Interaction`
- **Interfaces:** `IInteractable`
- **Propósito:** Interruptor interactuable en el escenario. Alterna su estado entre Activado/Desactivado al presionar la tecla E, cambia de color y dispara eventos `UnityEvent<bool>`.

#### Variables Serializadas (`[SerializeField]`):
| Variable | Tipo | Default | Descripción |
| :--- | :--- | :--- | :--- |
| `switchName` | `string` | `"Interruptor"` | Nombre descriptivo del interruptor. |
| `isActivated` | `bool` | `false` | Estado actual (true = encendido, false = apagado). |
| `targetRenderer` | `Renderer` | `null` | Renderer del objeto donde se aplica el color del estado. |
| `deactivatedColor` | `Color` | `Rojo` | Color cuando está desactivado. |
| `activatedColor` | `Color` | `Verde` | Color cuando está activado. |
| `proximityHighlightColor`| `Color` | `Amarillo` | Color de realce cuando el jugador se encuentra cerca. |
| `onStateChanged` | `UnityEvent<bool>` | - | Evento visualizable en el Inspector de Unity. |

---

### `CollectibleCube.cs`
- **Ubicación:** `Assets/Scripts/Interaction/CollectibleCube.cs`
- **Namespace:** `Scripts.Interaction`
- **Interfaces:** `IInteractable`
- **Propósito:** Objeto coleccionable. Genera un pulso visual oscilante cuando el jugador está en rango de interacción y se destruye al ser recogido con E.

#### Variables Serializadas (`[SerializeField]`):
- `itemName` (`string`, default: `"Cubo Coleccionable"`): Nombre del coleccionable en consola/UI.
- `normalColor` (`Color`, default: `Gris Claro`): Color en reposo.
- `highlightColor` (`Color`, default: `Amarillo Oro`): Color base del resplandor de proximidad.
- `pulseSpeed` (`float`, default: `4.0f`): Frecuencia de la animación de pulso senoidal.

---

## 6. Subflujo Datos & Perfiles (Flyweight)

### `MovementDataSO.cs`
- **Ubicación:** `Assets/Scripts/Data/MovementDataSO.cs`
- **Namespace:** `Scripts.Data`
- **Tipo:** `ScriptableObject`
- **Propósito:** Almacena todos los parámetros físicos y de postura de forma inmutable y compartida entre estados.

#### Variables Serializadas y Propiedades:
| Variable | Tipo | Default | Descripción |
| :--- | :--- | :--- | :--- |
| `walkSpeed` | `float` | `4.5f` | Velocidad de caminata en m/s. |
| `sprintSpeed` | `float` | `7.5f` | Velocidad de carrera en m/s. |
| `crouchSpeed` | `float` | `2.2f` | Velocidad agachado en m/s. |
| `proneSpeed` | `float` | `1.2f` | Velocidad cuerpo a tierra en m/s. |
| `slideInitialSpeed` | `float` | `9.0f` | Velocidad inicial al iniciar deslizamiento. |
| `rollSpeed` | `float` | `8.0f` | Velocidad constante de rodamiento evasivo. |
| `acceleration` | `float` | `12.0f` | Tasa de aceleración hacia la velocidad deseada. |
| `deceleration` | `float` | `14.0f` | Tasa de frenado hacia el reposo. |
| `rotationSmoothTime` | `float` | `0.1f` | Tiempo de amortiguación en la rotación del cuerpo. |
| `gravityMultiplier` | `float` | `2.0f` | Multiplicador de gravedad de Unity (9.81 * 2 = 19.62 m/s²). |
| `jumpForce` | `float` | `7.0f` | Impulso vertical inicial del salto. |
| `airControl` | `float` | `0.5f` | Porcentaje de maniobrabilidad horizontal en el aire (0 a 1). |
| `groundCheckRadius` | `float` | `0.25f` | Radio de la esfera de detección de suelo. |
| `groundCheckOffset` | `Vector3` | `(0, -0.9, 0)` | Offset inferior de chequeo de suelo. |
| `groundLayer` | `LayerMask` | `~0` | Capas que cuentan como superficie sólida. |
| `standingHeight` | `float` | `2.0f` | Altura de la cápsula de pie. |
| `standingCenter` | `Vector3` | `(0, 0, 0)` | Centro de la cápsula de pie. |
| `crouchingHeight` | `float` | `1.3f` | Altura de la cápsula agachado. |
| `crouchingCenter` | `Vector3` | `(0, -0.35, 0)`| Centro de la cápsula agachado. |
| `proneHeight` | `float` | `0.6f` | Altura de la cápsula cuerpo a tierra. |
| `proneCenter` | `Vector3` | `(0, -0.7, 0)` | Centro de la cápsula cuerpo a tierra. |
| `slideDuration` | `float` | `0.8f` | Duración del deslizamiento en segundos. |
| `rollDuration` | `float` | `0.6f` | Duración del rodillo evasivo en segundos. |
| `commandBufferDuration`| `float`| `0.35f` | Ventana de tiempo en que un comando en buffer permanece vivo. |

---

### `CharacterDataSO.cs`
- **Ubicación:** `Assets/Scripts/Data/CharacterDataSO.cs`
- **Namespace:** `Scripts.Data`
- **Tipo:** `ScriptableObject`
- **Propósito:** Perfil completo de personaje según el patrón Flyweight. Permite definir personajes con diferentes modelos, parámetros de movimiento y atributos de combate sin duplicar memoria.

#### Variables Serializadas y Propiedades:
- `characterId` (`string`, default: `"character_default"`): Identificador único.
- `characterDisplayName` (`string`, default: `"Standard Operative"`): Nombre público.
- `characterDescription` (`string`): Descripción narrativa o táctica.
- `visualPrefab` (`GameObject`): Prefab del modelo 3D del personaje.
- `animatorController` (`RuntimeAnimatorController`): Controlador de animaciones.
- `characterAvatar` (`Avatar`): Configuración de esqueleto humanoide.
- `movementParameters` (`MovementDataSO`): Parámetros de locomoción vinculados.
- `maxHealth` (`float`, default: `100.0f`): Vida máxima del personaje.
- `maxStamina` (`float`, default: `100.0f`): Resistencia máxima.

---

## 7. Interfaces del Core

Todas ubicadas en `Assets/Scripts/Core/`:

### `IState.cs`
```csharp
public interface IState
{
    void Enter();
    void Execute();
    void FixedExecute();
    void Exit();
}
```

### `ICommand.cs`
```csharp
public interface ICommand
{
    void Execute();
    bool CanExecute();
}
```

### `IDamageable.cs`
```csharp
public interface IDamageable
{
    void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection);
    bool IsAlive();
}
```

### `IInteractable.cs`
```csharp
public interface IInteractable
{
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
    string GetInteractionPrompt();
    void OnFocusGained();
    void OnFocusLost();
}
```

---

## 8. Scripts Auxiliares y Legado

### `Target.cs`
- **Ubicación:** `Assets/Other/Target.cs`
- **Propósito:** Diana de pruebas para armas de combate. Cuenta con 100 puntos de salud y se destruye al recibir suficiente daño.
- **Variables:** `MaxHealth` (`int`, default: `100`).
- **Métodos:** `TakeDamage(int amount)`.

### `PlayerController.cs`
- **Ubicación:** `Assets/Scripts/PlayerController.cs`
- **Propósito:** Script monolítico del prototipo original. Se conserva intacto para fines de comparación histórica o respaldo, habiendo sido superado por la nueva arquitectura modular con State Pattern y Flyweight SO.
