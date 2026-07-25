using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    #region Configurações de Movimento
    [Header("Configurações de Movimento")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 9f;
    [SerializeField] private float crouchSpeed = 3.5f;
    [SerializeField] private float proneSpeed = 1.5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravidadeExtra = 30f;
    #endregion

    #region Configurações de Física
    [Header("Camadas de Apoio")]
    [SerializeField] private LayerMask layerChao;
    [SerializeField] private LayerMask layerObstaculo;
    #endregion

    #region Configurações de Rolamento
    [Header("Configurações de Rolamento (Esquiva)")]
    [SerializeField] private float velocidadeRolamento = 14f;
    [SerializeField] private float tempoRolamento = 0.85f;
    [SerializeField] private float cooldownEsquiva = 0.45f;
    private float tempoProximaEsquiva = 0f;
    #endregion

    #region Sistema de Combate (Melee)
    [Header("Configurações de Combate")]
    [SerializeField] private GameObject armaNaMao;
    [SerializeField] private GameObject armaNasCostas;

    [Space]
    [SerializeField] private float tempoEquipar = 1.2f;
    [SerializeField] private float momentoDePegarArma = 0.5f;
    [SerializeField] private float tempoAtaqueLeve = 0.8f;
    [SerializeField] private float tempoAtaquePesado = 1.5f;

    private bool isArmed = false;
    private bool isAttacking = false;
    private bool isEquipping = false;
    private int lightComboIndex = 0;
    private int heavyComboIndex = 0;
    #endregion

    #region Inércia e Colisor
    [Header("Inércia e Frenagem")]
    [SerializeField] private float aceleracaoCorrida = 10f;
    [SerializeField] private float frenagemCaminhada = 14f;
    [SerializeField] private float frenagemCorrida = 6f;

    [Header("Ajuste Dinâmico do Colisor")]
    [SerializeField] private bool ajustarColisorDinamico = true;
    [SerializeField] private float alturaEmPe = 2f;
    [SerializeField] private float alturaAgachado = 1.2f;
    [SerializeField] private float alturaDeitado = 0.4f;
    #endregion

    #region Câmera
    [Header("Configurações da Câmera")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("Posição ideal da câmera. 0 no X garante que ela fique reta com o teclado.")]
    [SerializeField] private Vector3 offsetCamera = new Vector3(0f, 15f, -8f);

    [SerializeField] private bool usarScrollDoMouse = true;
    [SerializeField] private KeyCode botaoZoomIn = KeyCode.Equals;
    [SerializeField] private KeyCode botaoZoomOut = KeyCode.Minus;

    [Space]
    [SerializeField] private float distanciaMinima = 5f;
    [SerializeField] private float distanciaMaxima = 25f;
    [SerializeField] private float sensibilidadeZoom = 5f;
    [SerializeField] private float suavidadeZoom = 10f;
    #endregion

    #region Variáveis Privadas
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private Animator anim;

    private float distanciaAlvo;
    private float distanciaAtual;
    private Vector3 direcaoOriginalDaCamera;
    private Plane groundPlane;

    private Vector3 moveInput;
    private Vector3 smoothedMoveInput;
    private float currentSpeed;
    private float currentAnimMultiplier = 1f;
    private float valorGiro = 0f;
    private float centroYOriginal;

    private bool isCrouching = false;
    private bool isProne = false;
    private bool querCorrer = false;
    private bool visualCorrida = false;
    private bool estaEsquivando = false;
    private bool mudandoPostura = false;
    private float tempoBloqueioQueda = 0f;

    // Variável para calcular a calçada
    private float tempoNoAr = 0f;
    #endregion

    #region Hashes do Animator
    private static readonly int hashIsCrouching = Animator.StringToHash("isCrouching");
    private static readonly int hashIsProne = Animator.StringToHash("isProne");
    private static readonly int hashIsSprinting = Animator.StringToHash("isSprinting");
    private static readonly int hashIsFalling = Animator.StringToHash("isFalling");
    private static readonly int hashVelocityX = Animator.StringToHash("VelocityX");
    private static readonly int hashVelocityZ = Animator.StringToHash("VelocityZ");
    private static readonly int hashTurn = Animator.StringToHash("Turn");

    private static readonly int hashRoll = Animator.StringToHash("Roll");
    private static readonly int hashEquip = Animator.StringToHash("Equip");
    private static readonly int hashLightAttack = Animator.StringToHash("LightAttack");
    private static readonly int hashHeavyAttack = Animator.StringToHash("HeavyAttack");
    private static readonly int hashLightAttackIndex = Animator.StringToHash("LightAttackIndex");
    private static readonly int hashHeavyAttackIndex = Animator.StringToHash("HeavyAttackIndex");
    private static readonly int hashIsArmed = Animator.StringToHash("IsArmed");
    #endregion

    void Start()
    {
        InicializarComponentes();
        InicializarCamera();

        if (armaNaMao != null) armaNaMao.SetActive(false);
        if (armaNasCostas != null) armaNasCostas.SetActive(true);
    }

    void Update()
    {
        ProcessarInputsDeCombate();
        ProcessarInputsDeEstado();
        CalcularMovimentoFisico();
        CalcularRotacaoMouse();
        HandleZoomInput();

        if (ajustarColisorDinamico) RedimensionarColisorDoPlayer();

        AtualizarAnimator();
    }

    void FixedUpdate()
    {
        Vector3 novaVelocidade = smoothedMoveInput * currentSpeed;
        novaVelocidade.y = rb.linearVelocity.y;

        if (!isGrounded())
        {
            novaVelocidade.y -= gravidadeExtra * Time.fixedDeltaTime;
        }

        rb.linearVelocity = novaVelocidade;
    }

    void LateUpdate()
    {
        if (playerCamera != null) AplicarZoomECameraFollow();
    }

    #region Lógica de Combate e Estados
    private void ProcessarInputsDeCombate()
    {
        if (estaEsquivando || isAttacking || isEquipping) return;

        if (Input.GetKeyDown(KeyCode.E)) StartCoroutine(RotinaEquiparArma());

        if (isArmed && isGrounded())
        {
            if (Input.GetMouseButtonDown(0)) StartCoroutine(RotinaAtaque(true));
            else if (Input.GetMouseButtonDown(1)) StartCoroutine(RotinaAtaque(false));
        }
    }

    private IEnumerator RotinaEquiparArma()
    {
        isEquipping = true;
        isCrouching = false;
        isProne = false;
        smoothedMoveInput = Vector3.zero;

        if (anim != null) anim.SetTrigger(hashEquip);
        yield return new WaitForSeconds(momentoDePegarArma);
        isArmed = !isArmed;

        if (armaNaMao != null) armaNaMao.SetActive(isArmed);
        if (armaNasCostas != null) armaNasCostas.SetActive(!isArmed);

        yield return new WaitForSeconds(tempoEquipar - momentoDePegarArma);
        isEquipping = false;
    }

    private IEnumerator RotinaAtaque(bool isLightAttack)
    {
        isAttacking = true;
        smoothedMoveInput = Vector3.zero;
        CalcularRotacaoMouse(true);

        if (isLightAttack)
        {
            if (anim != null) { anim.SetInteger(hashLightAttackIndex, lightComboIndex); anim.SetTrigger(hashLightAttack); }
            lightComboIndex++;
            if (lightComboIndex > 2) lightComboIndex = 0;
            yield return new WaitForSeconds(tempoAtaqueLeve);
        }
        else
        {
            if (anim != null) { anim.SetInteger(hashHeavyAttackIndex, heavyComboIndex); anim.SetTrigger(hashHeavyAttack); }
            heavyComboIndex++;
            if (heavyComboIndex > 1) heavyComboIndex = 0;
            yield return new WaitForSeconds(tempoAtaquePesado);
        }

        isAttacking = false;
    }

    private bool isGrounded()
    {
        if (capsuleCollider == null) return false;
        float baseDaCapsula = capsuleCollider.bounds.min.y;
        Vector3 solaDoPe = new Vector3(capsuleCollider.bounds.center.x, baseDaCapsula, capsuleCollider.bounds.center.z);
        Vector3 sensor = solaDoPe + (Vector3.up * 0.1f);
        LayerMask camadasDeApoio = layerChao | layerObstaculo;
        return Physics.CheckSphere(sensor, 0.3f, camadasDeApoio, QueryTriggerInteraction.Ignore);
    }
    #endregion

    #region Movimentação e Rolamento
    private void ProcessarInputsDeEstado()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.z = Input.GetAxisRaw("Vertical");
        Vector3 targetMoveInput = moveInput.normalized;
        querCorrer = Input.GetKey(KeyCode.LeftShift) && targetMoveInput.magnitude > 0;

        if (estaEsquivando || isAttacking || isEquipping) return;

        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= tempoProximaEsquiva)
        {
            tempoProximaEsquiva = Time.time + tempoRolamento + cooldownEsquiva;
            StartCoroutine(ExecutarRolamento());
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            tempoBloqueioQueda = Time.time + 0.6f;
            if (isProne) { isProne = false; isCrouching = true; }
            else { isCrouching = !isCrouching; }
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            tempoBloqueioQueda = Time.time + 0.6f;
            if (isCrouching || !isProne) { isProne = true; isCrouching = false; }
            else { isProne = false; }
        }

        bool estaDeslizando = isCrouching && currentSpeed > walkSpeed + 0.5f;
        if (querCorrer && !estaDeslizando) { isCrouching = false; isProne = false; }
    }

    private IEnumerator ExecutarRolamento()
    {
        estaEsquivando = true;
        isCrouching = false;
        isProne = false;

        if (anim != null) anim.SetTrigger(hashRoll);

        Vector3 direcaoRolamento = moveInput.normalized;
        if (direcaoRolamento.magnitude < 0.1f) direcaoRolamento = transform.forward;
        transform.rotation = Quaternion.LookRotation(direcaoRolamento);

        float tempoDecorrido = 0f;
        while (tempoDecorrido < tempoRolamento)
        {
            if (!isGrounded() && tempoDecorrido > 0.2f) break;

            smoothedMoveInput = direcaoRolamento;
            float progresso = tempoDecorrido / tempoRolamento;

            if (progresso <= 0.4f)
            {
                currentSpeed = velocidadeRolamento;
            }
            else
            {
                float progressoFrenagem = (progresso - 0.4f) / 0.6f;
                float velocidadeFinal = querCorrer ? sprintSpeed : walkSpeed;
                currentSpeed = Mathf.Lerp(velocidadeRolamento, velocidadeFinal, progressoFrenagem);
            }

            tempoDecorrido += Time.deltaTime;
            yield return null;
        }

        currentSpeed = querCorrer ? sprintSpeed : walkSpeed;
        estaEsquivando = false;
    }

    private void CalcularMovimentoFisico()
    {
        if (estaEsquivando) return;

        if (isAttacking || isEquipping)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, 20f * Time.deltaTime);
            smoothedMoveInput = Vector3.zero;
            return;
        }

        Vector3 targetMoveInput = moveInput.normalized;
        float taxaFrenagemAtual = frenagemCaminhada;
        if (targetMoveInput.magnitude == 0 && currentSpeed > walkSpeed + 0.5f) taxaFrenagemAtual = frenagemCorrida;

        smoothedMoveInput = Vector3.MoveTowards(smoothedMoveInput, targetMoveInput, taxaFrenagemAtual * Time.deltaTime);

        float targetSpeed = walkSpeed;
        if (isProne) targetSpeed = proneSpeed;
        else if (isCrouching) targetSpeed = crouchSpeed;
        else if (querCorrer) targetSpeed = sprintSpeed;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, aceleracaoCorrida * Time.deltaTime);

        if (mudandoPostura) currentSpeed = Mathf.MoveTowards(currentSpeed, proneSpeed, 30f * Time.deltaTime);
    }

    private void CalcularRotacaoMouse(bool forcarGiro = false)
    {
        if (playerCamera == null || estaEsquivando || isEquipping || (isAttacking && !forcarGiro)) return;

        float rotacaoInicialY = transform.eulerAngles.y;
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        groundPlane.SetNormalAndPosition(Vector3.up, new Vector3(0, transform.position.y, 0));

        if (groundPlane.Raycast(ray, out float rayLength))
        {
            Vector3 pointToLook = ray.GetPoint(rayLength);
            Vector3 direction = pointToLook - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 2f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                if (forcarGiro) transform.rotation = targetRotation;
                else transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        float deltaRotacao = Mathf.DeltaAngle(rotacaoInicialY, transform.eulerAngles.y);
        valorGiro = (Time.deltaTime > 0 && !isAttacking) ? Mathf.Clamp(deltaRotacao / (rotationSpeed * Time.deltaTime), -1f, 1f) : 0f;
        if (moveInput.magnitude > 0) valorGiro = 0f;
    }

    private void AtualizarAnimator()
    {
        if (anim == null) return;

        visualCorrida = (querCorrer || (currentSpeed > walkSpeed + 0.5f && smoothedMoveInput.magnitude > 0.05f)) && !isCrouching && !isProne && !isAttacking && !isEquipping;

        anim.SetBool(hashIsCrouching, isCrouching);
        anim.SetBool(hashIsProne, isProne);
        anim.SetBool(hashIsSprinting, visualCorrida);
        anim.SetBool(hashIsArmed, isArmed);

        // 🚨 A MÁGICA DA CALÇADA: Cronômetro de tempo no ar
        if (!isGrounded())
        {
            tempoNoAr += Time.deltaTime;
        }
        else
        {
            tempoNoAr = 0f;
        }

        bool deveCair = (tempoNoAr > 0.30f) && !isProne && !estaEsquivando && !mudandoPostura && Time.time > tempoBloqueioQueda;
        anim.SetBool(hashIsFalling, deveCair);
        // ----------------------------------------------------

        Vector3 localMove = transform.InverseTransformDirection(smoothedMoveInput);
        float alvoMultiplicador = visualCorrida ? 2f : 1f;
        currentAnimMultiplier = Mathf.MoveTowards(currentAnimMultiplier, alvoMultiplicador, aceleracaoCorrida * 0.5f * Time.deltaTime);

        float finalX = localMove.x * currentAnimMultiplier;
        float finalZ = localMove.z * currentAnimMultiplier;

        if (isProne) { finalZ = moveInput.magnitude > 0.01f ? smoothedMoveInput.magnitude * currentAnimMultiplier : 0f; finalX = 0f; }

        anim.SetFloat(hashVelocityX, finalX, 0.05f, Time.deltaTime);
        anim.SetFloat(hashVelocityZ, finalZ, 0.05f, Time.deltaTime);
        anim.SetFloat(hashTurn, valorGiro, 0.1f, Time.deltaTime);
    }
    #endregion

    #region Métodos Auxiliares
    private void InicializarComponentes()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        anim = GetComponentInChildren<Animator>();
        rb.freezeRotation = true;
        currentSpeed = walkSpeed;
        smoothedMoveInput = Vector3.zero;
        if (capsuleCollider != null) { alturaEmPe = capsuleCollider.height; centroYOriginal = capsuleCollider.center.y; }
    }

    private void InicializarCamera()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera != null)
        {
            distanciaAtual = offsetCamera.magnitude;
            distanciaAlvo = distanciaAtual;
            direcaoOriginalDaCamera = offsetCamera.normalized;
        }
    }

    private void HandleZoomInput()
    {
        float inputDeZoom = 0f;
        if (usarScrollDoMouse) inputDeZoom = Input.GetAxis("Mouse ScrollWheel") * -1f * sensibilidadeZoom * 10f;
        if (Input.GetKey(botaoZoomIn)) inputDeZoom = -sensibilidadeZoom * Time.deltaTime;
        else if (Input.GetKey(botaoZoomOut)) inputDeZoom = sensibilidadeZoom * Time.deltaTime;
        if (inputDeZoom != 0)
        {
            distanciaAlvo += inputDeZoom;
            distanciaAlvo = Mathf.Clamp(distanciaAlvo, distanciaMinima, distanciaMaxima);
        }
    }

    private void AplicarZoomECameraFollow()
    {
        distanciaAtual = Mathf.Lerp(distanciaAtual, distanciaAlvo, suavidadeZoom * Time.deltaTime);
        Vector3 novaPosicaoCamera = transform.position + (direcaoOriginalDaCamera * distanciaAtual);
        playerCamera.transform.position = novaPosicaoCamera;
        playerCamera.transform.LookAt(transform.position + Vector3.up * (alturaEmPe / 2f));
    }

    private void RedimensionarColisorDoPlayer()
    {
        if (capsuleCollider == null) return;

        float alturaAlvo = alturaEmPe;
        if (isProne) alturaAlvo = alturaDeitado;
        else if (isCrouching) alturaAlvo = alturaAgachado;

        mudandoPostura = Mathf.Abs(capsuleCollider.height - alturaAlvo) > 0.05f;

        capsuleCollider.height = Mathf.Lerp(capsuleCollider.height, alturaAlvo, 15f * Time.deltaTime);
        float diferencaAltura = alturaEmPe - capsuleCollider.height;
        Vector3 novoCentro = capsuleCollider.center;
        novoCentro.y = centroYOriginal - (diferencaAltura / 2f);
        capsuleCollider.center = novoCentro;
    }
    #endregion
}