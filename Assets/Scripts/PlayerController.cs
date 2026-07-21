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
    [SerializeField] private float rotationSpeed = 10f;
    #endregion

    #region Configurações de Física
    [Header("Configurações de Física e Chão")]
    [SerializeField] private LayerMask layerChao; // 🚨 Lembre-se de marcar a layer do seu chão/cenário aqui no Inspector!
    #endregion

    #region Configurações de Rolamento (Esquiva)
    [Header("Configurações de Rolamento (Esquiva)")]
    [SerializeField] private float velocidadeRolamento = 10f;
    [SerializeField] private float tempoRolamento = 0.85f;
    [SerializeField] private float cooldownRolamento = 1.0f;

    private float tempoProximoRolamento = 0f;
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

    #region Configurações de Inércia e Frenagem
    [Header("Configurações de Inércia e Frenagem")]
    [SerializeField] private float aceleracaoCorrida = 8f;
    [SerializeField] private float frenagemCaminhada = 12f;
    [SerializeField] private float frenagemCorrida = 4f;
    #endregion

    #region Configurações do Colisor
    [Header("Ajuste Dinâmico do Colisor")]
    [SerializeField] private bool ajustarColisorDinamico = true;
    [SerializeField] private float alturaEmPe = 2f;
    [SerializeField] private float alturaAgachado = 1.2f;
    [SerializeField] private float alturaDeitado = 0.4f;
    #endregion

    #region Configurações da Câmera
    [Header("Configurações da Câmera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private bool usarScrollDoMouse = true;
    [SerializeField] private KeyCode botaoZoomIn = KeyCode.Equals;
    [SerializeField] private KeyCode botaoZoomOut = KeyCode.Minus;

    [Space]
    [SerializeField] private float distanciaMinima = 5f;
    [SerializeField] private float distanciaMaxima = 25f;
    [SerializeField] private float sensibilidadeZoom = 5f;
    [SerializeField] private float suavidadeZoom = 10f;
    #endregion

    #region Variáveis Privadas de Controle
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
    #endregion

    #region Otimização do Animator (Hashes)
    private static readonly int hashIsCrouching = Animator.StringToHash("isCrouching");
    private static readonly int hashIsProne = Animator.StringToHash("isProne");
    private static readonly int hashIsSprinting = Animator.StringToHash("isSprinting");
    private static readonly int hashIsFalling = Animator.StringToHash("isFalling");
    private static readonly int hashVelocityX = Animator.StringToHash("VelocityX");
    private static readonly int hashVelocityZ = Animator.StringToHash("VelocityZ");
    private static readonly int hashTurn = Animator.StringToHash("Turn");
    private static readonly int hashRoll = Animator.StringToHash("Roll");

    // Hashes de Combate
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
        if (estaEsquivando) return;

        // Física real que respeita a gravidade e permite cair do bloco
        Vector3 novaVelocidade = smoothedMoveInput * currentSpeed;
        novaVelocidade.y = rb.linearVelocity.y;
        rb.linearVelocity = novaVelocidade;
    }

    void LateUpdate()
    {
        if (playerCamera != null) AplicarZoomECameraFollow();
    }

    #region Lógica de Combate
    private void ProcessarInputsDeCombate()
    {
        if (estaEsquivando || isAttacking || isEquipping) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            StartCoroutine(RotinaEquiparArma());
        }

        if (isArmed && isGrounded())
        {
            if (Input.GetMouseButtonDown(0))
            {
                StartCoroutine(RotinaAtaque(true));
            }
            else if (Input.GetMouseButtonDown(1))
            {
                StartCoroutine(RotinaAtaque(false));
            }
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
            if (anim != null)
            {
                anim.SetInteger(hashLightAttackIndex, lightComboIndex);
                anim.SetTrigger(hashLightAttack);
            }

            lightComboIndex++;
            if (lightComboIndex > 2) lightComboIndex = 0;

            yield return new WaitForSeconds(tempoAtaqueLeve);
        }
        else
        {
            if (anim != null)
            {
                anim.SetInteger(hashHeavyAttackIndex, heavyComboIndex);
                anim.SetTrigger(hashHeavyAttack);
            }

            heavyComboIndex++;
            if (heavyComboIndex > 1) heavyComboIndex = 0;

            yield return new WaitForSeconds(tempoAtaquePesado);
        }

        isAttacking = false;
    }

    // 🚨 ALTERAÇÃO: Sensor inteligente que acha o pé independentemente da altura do personagem
    private bool isGrounded()
    {
        if (capsuleCollider == null) return false;

        // Calcula exatamente onde é a sola do pé usando o tamanho do seu colisor
        Vector3 solaDoPe = transform.position + capsuleCollider.center - (Vector3.up * (capsuleCollider.height / 2f));

        // Sobe o sensor um pouquinho (0.1f) para a esfera não ficar metade afundada no chão
        Vector3 sensor = solaDoPe + Vector3.up * 0.1f;

        // Cria a esfera de detecção
        return Physics.CheckSphere(sensor, 0.25f, layerChao);
    }
    #endregion

    #region Métodos de Lógica Principal

    private void ProcessarInputsDeEstado()
    {
        if (estaEsquivando || isAttacking || isEquipping) return;

        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= tempoProximoRolamento)
        {
            tempoProximoRolamento = Time.time + tempoRolamento + cooldownRolamento;
            StartCoroutine(ExecutarRolamento());
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            if (isProne) { isProne = false; isCrouching = true; }
            else { isCrouching = !isCrouching; }
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (isCrouching || !isProne) { isProne = true; isCrouching = false; }
            else { isProne = false; }
        }

        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.z = Input.GetAxisRaw("Vertical");
        Vector3 targetMoveInput = moveInput.normalized;

        querCorrer = Input.GetKey(KeyCode.LeftShift) && targetMoveInput.magnitude > 0;
        bool estaDeslizando = isCrouching && currentSpeed > walkSpeed + 0.5f;

        if (querCorrer && !estaDeslizando)
        {
            isCrouching = false;
            isProne = false;
        }
    }

    private IEnumerator ExecutarRolamento()
    {
        estaEsquivando = true;
        isCrouching = false;
        isProne = false;

        if (anim != null) anim.SetTrigger(hashRoll);

        Vector3 direcaoRolamento = moveInput.normalized;
        if (direcaoRolamento.magnitude < 0.1f)
        {
            direcaoRolamento = transform.forward;
        }

        transform.rotation = Quaternion.LookRotation(direcaoRolamento);
        smoothedMoveInput = direcaoRolamento;
        currentSpeed = velocidadeRolamento;

        yield return new WaitForSeconds(tempoRolamento);

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
        if (targetMoveInput.magnitude == 0 && currentSpeed > walkSpeed + 0.5f)
        {
            taxaFrenagemAtual = frenagemCorrida;
        }

        smoothedMoveInput = Vector3.MoveTowards(smoothedMoveInput, targetMoveInput, taxaFrenagemAtual * Time.deltaTime);

        float targetSpeed = walkSpeed;
        if (isProne) targetSpeed = proneSpeed;
        else if (isCrouching) targetSpeed = crouchSpeed;
        else if (querCorrer) targetSpeed = sprintSpeed;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, aceleracaoCorrida * Time.deltaTime);

        if (anim != null)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo nextStateInfo = anim.GetNextAnimatorStateInfo(0);

            bool emTransicaoDeChao = stateInfo.IsName("ProneToStand") || nextStateInfo.IsName("ProneToStand") ||
                                     stateInfo.IsName("TransitionToProne") || nextStateInfo.IsName("TransitionToProne");

            if (emTransicaoDeChao)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, proneSpeed, 30f * Time.deltaTime);
            }
        }
    }

    private void CalcularRotacaoMouse(bool forcarGiro = false)
    {
        if (playerCamera == null) return;
        if (estaEsquivando || isEquipping) return;
        if (isAttacking && !forcarGiro) return;

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
        valorGiro = 0f;

        if (Time.deltaTime > 0 && !isAttacking)
            valorGiro = Mathf.Clamp(deltaRotacao / (rotationSpeed * Time.deltaTime), -1f, 1f);

        if (moveInput.magnitude > 0)
            valorGiro = 0f;
    }

    private void AtualizarAnimator()
    {
        if (anim == null) return;

        visualCorrida = (querCorrer || (currentSpeed > walkSpeed + 0.5f && smoothedMoveInput.magnitude > 0.05f)) && !isCrouching && !isProne && !isAttacking && !isEquipping;

        anim.SetBool(hashIsCrouching, isCrouching);
        anim.SetBool(hashIsProne, isProne);
        anim.SetBool(hashIsSprinting, visualCorrida);
        anim.SetBool(hashIsArmed, isArmed);

        // Envia o status de queda para o Animator
        anim.SetBool(hashIsFalling, !isGrounded());

        Vector3 localMove = transform.InverseTransformDirection(smoothedMoveInput);
        float alvoMultiplicador = visualCorrida ? 2f : 1f;

        currentAnimMultiplier = Mathf.MoveTowards(currentAnimMultiplier, alvoMultiplicador, aceleracaoCorrida * 0.5f * Time.deltaTime);

        float finalX = localMove.x * currentAnimMultiplier;
        float finalZ = localMove.z * currentAnimMultiplier;

        if (isProne)
        {
            if (moveInput.magnitude > 0.01f) finalZ = smoothedMoveInput.magnitude * currentAnimMultiplier;
            else finalZ = 0f;
            finalX = 0f;
        }

        anim.SetFloat(hashVelocityX, finalX, 0.05f, Time.deltaTime);
        anim.SetFloat(hashVelocityZ, finalZ, 0.05f, Time.deltaTime);
        anim.SetFloat(hashTurn, valorGiro, 0.1f, Time.deltaTime);
    }
    #endregion

    #region Métodos Auxiliares
    private void InicializarComponentes() { rb = GetComponent<Rigidbody>(); capsuleCollider = GetComponent<CapsuleCollider>(); anim = GetComponentInChildren<Animator>(); rb.freezeRotation = true; currentSpeed = walkSpeed; smoothedMoveInput = Vector3.zero; if (capsuleCollider != null) { alturaEmPe = capsuleCollider.height; centroYOriginal = capsuleCollider.center.y; } }
    private void InicializarCamera() { if (playerCamera == null) playerCamera = Camera.main; if (playerCamera != null) { Vector3 offsetInicial = playerCamera.transform.position - transform.position; distanciaAtual = offsetInicial.magnitude; distanciaAlvo = distanciaAtual; direcaoOriginalDaCamera = offsetInicial.normalized; } }
    private void HandleZoomInput() { float inputDeZoom = 0f; if (usarScrollDoMouse) inputDeZoom = Input.GetAxis("Mouse ScrollWheel") * -1f * sensibilidadeZoom * 10f; if (Input.GetKey(botaoZoomIn)) inputDeZoom = -sensibilidadeZoom * Time.deltaTime; else if (Input.GetKey(botaoZoomOut)) inputDeZoom = sensibilidadeZoom * Time.deltaTime; if (inputDeZoom != 0) { distanciaAlvo += inputDeZoom; distanciaAlvo = Mathf.Clamp(distanciaAlvo, distanciaMinima, distanciaMaxima); } }
    private void AplicarZoomECameraFollow() { distanciaAtual = Mathf.Lerp(distanciaAtual, distanciaAlvo, suavidadeZoom * Time.deltaTime); Vector3 novaPosicaoCamera = transform.position + (direcaoOriginalDaCamera * distanciaAtual); playerCamera.transform.position = novaPosicaoCamera; playerCamera.transform.LookAt(transform.position + Vector3.up * (alturaEmPe / 2f)); }
    private void RedimensionarColisorDoPlayer() { if (capsuleCollider == null) return; float alturaAlvo = alturaEmPe; bool estaLevantando = false; if (anim != null) { estaLevantando = anim.GetCurrentAnimatorStateInfo(0).IsName("ProneToStand") || anim.GetNextAnimatorStateInfo(0).IsName("ProneToStand"); } if (isProne) alturaAlvo = alturaDeitado; else if (estaLevantando) alturaAlvo = alturaAgachado; else if (isCrouching) alturaAlvo = alturaAgachado; capsuleCollider.height = Mathf.Lerp(capsuleCollider.height, alturaAlvo, 10f * Time.deltaTime); float diferencaAltura = alturaEmPe - capsuleCollider.height; Vector3 novoCentro = capsuleCollider.center; novoCentro.y = centroYOriginal - (diferencaAltura / 2f); capsuleCollider.center = novoCentro; }
    #endregion
}