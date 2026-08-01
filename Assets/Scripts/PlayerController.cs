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
    [Header("Camadas de Apoio e Obstáculos")]
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

    #region Sistema de Combate e Armas
    [Header("Gerenciador de Armas")]
    [Tooltip("0 = Desarmado | 1 = Machete | 2 = Pistola")]
    public int armaAtual = 0;
    private bool isArmed = false;
    private bool isAttacking = false;
    private bool isEquipping = false;
    private bool isAiming = false;

    [Header("Slots: Machete")]
    [SerializeField] private GameObject macheteNaMao;
    [SerializeField] private GameObject macheteNasCostas;
    [SerializeField] private Transform pontoDeAtaqueMelee;
    [SerializeField] private float raioDoAtaque = 1.2f;
    [SerializeField] private int danoAtaqueLeve = 25;
    [SerializeField] private int danoAtaquePesado = 50;
    [SerializeField] private float tempoAtaqueLeve = 0.8f;
    [SerializeField] private float tempoAtaquePesado = 1.5f;
    [Range(0.1f, 0.9f)][SerializeField] private float porcentagemHitLeve = 0.4f;
    [Range(0.1f, 0.9f)][SerializeField] private float porcentagemHitPesado = 0.5f;

    [Header("Slots: Pistola")]
    [SerializeField] private GameObject pistolaNaMao;
    [SerializeField] private GameObject pistolaNoColdre;
    [SerializeField] private Transform pontoDeDisparo;
    [SerializeField] private LineRenderer miraLaser;
    [SerializeField] private int danoPistola = 25;
    [SerializeField] private float tempoEntreTiros = 0.3f;
    [SerializeField] private float alcanceTiro = 50f;
    [SerializeField] private ParticleSystem efeitoFogoPistola;

    [Header("Efeitos Visuais (VFX)")]
    [SerializeField] private GameObject efeitoSanguePrefab; // Prefab do sangue

    [Header("Configurações Gerais de Combate")]
    [SerializeField] private LayerMask layerInimigos;
    [SerializeField] private float tempoEquipar = 1.2f;
    [SerializeField] private float momentoDePegarArma = 0.5f;

    [Header("Efeitos de Impacto (Camera Shake)")]
    [SerializeField] private float forcaCameraShake = 0.3f;
    [SerializeField] private float tempoCameraShake = 0.15f;
    private Vector3 cameraShakeOffset = Vector3.zero;

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
    [SerializeField] private Vector3 offsetCamera = new Vector3(0f, 15f, -8f);
    [SerializeField] private bool usarScrollDoMouse = true;
    [SerializeField] private KeyCode botaoZoomIn = KeyCode.Equals;
    [SerializeField] private KeyCode botaoZoomOut = KeyCode.Minus;
    [SerializeField] private float distanciaMinima = 5f;
    [SerializeField] private float distanciaMaxima = 25f;
    [SerializeField] private float sensibilidadeZoom = 5f;
    [SerializeField] private float suavidadeZoom = 10f;
    #endregion

    #region Variáveis Privadas
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private Animator anim;
    private float distanciaAlvo, distanciaAtual;
    private Vector3 direcaoOriginalDaCamera;
    private Plane groundPlane;
    private Vector3 moveInput, smoothedMoveInput;
    private float currentSpeed, valorGiro, centroYOriginal;
    private bool isCrouching, isProne, querCorrer, visualCorrida, estaEsquivando, mudandoPostura;
    private float tempoNoAr, tempoBloqueioQueda;
    private float currentAnimMultiplier = 1f;
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

    private static readonly int hashTipoArma = Animator.StringToHash("TipoArma");
    private static readonly int hashEquip = Animator.StringToHash("Equip");
    private static readonly int hashLightAttack = Animator.StringToHash("LightAttack");
    private static readonly int hashHeavyAttack = Animator.StringToHash("HeavyAttack");
    private static readonly int hashLightAttackIndex = Animator.StringToHash("LightAttackIndex");
    private static readonly int hashHeavyAttackIndex = Animator.StringToHash("HeavyAttackIndex");
    private static readonly int hashIsArmed = Animator.StringToHash("IsArmed");
    private static readonly int hashAtirar = Animator.StringToHash("Atirar");
    private static readonly int hashIsAiming = Animator.StringToHash("IsAiming");
    #endregion

    void Start()
    {
        InicializarComponentes();
        InicializarCamera();

        // Garante que o jogo sempre inicie desarmado
        armaAtual = 0;
        isArmed = false;
        if (anim != null) anim.SetInteger(hashTipoArma, 0);

        DesligarTodasAsArmas();

        if (miraLaser != null) miraLaser.enabled = false;
    }

    void Update()
    {
        ProcessarTrocaDeArmas();
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
        if (!isGrounded()) novaVelocidade.y -= gravidadeExtra * Time.fixedDeltaTime;
        rb.linearVelocity = novaVelocidade;
    }

    void LateUpdate()
    {
        if (playerCamera != null) AplicarZoomECameraFollow();
    }

    #region Gerenciador de Armas
    private void DesligarTodasAsArmas()
    {
        if (macheteNaMao != null) macheteNaMao.SetActive(false);
        if (macheteNasCostas != null) macheteNasCostas.SetActive(true);
        if (pistolaNaMao != null) pistolaNaMao.SetActive(false);
        if (pistolaNoColdre != null) pistolaNoColdre.SetActive(true);
    }

    private void ProcessarTrocaDeArmas()
    {
        if (estaEsquivando || isAttacking || isEquipping) return;

        if (Input.GetKeyDown(KeyCode.Alpha1) && armaAtual != 1)
        {
            armaAtual = 1;
            if (isArmed) StartCoroutine(RotinaTrocarArmaDireto());
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) && armaAtual != 2)
        {
            armaAtual = 2;
            if (isArmed) StartCoroutine(RotinaTrocarArmaDireto());
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            StartCoroutine(RotinaEquiparArma());
        }
    }

    private IEnumerator RotinaTrocarArmaDireto()
    {
        isEquipping = true;
        isAiming = false;
        if (miraLaser != null) miraLaser.enabled = false;
        smoothedMoveInput = Vector3.zero;
        DesligarTodasAsArmas();

        if (anim != null) anim.SetInteger(hashTipoArma, armaAtual);
        if (anim != null) anim.SetTrigger(hashEquip);

        yield return new WaitForSeconds(momentoDePegarArma);

        if (armaAtual == 1) { if (macheteNaMao != null) macheteNaMao.SetActive(true); if (macheteNasCostas != null) macheteNasCostas.SetActive(false); }
        else if (armaAtual == 2) { if (pistolaNaMao != null) pistolaNaMao.SetActive(true); if (pistolaNoColdre != null) pistolaNoColdre.SetActive(false); }

        yield return new WaitForSeconds(tempoEquipar - momentoDePegarArma);
        isEquipping = false;
    }

    private IEnumerator RotinaEquiparArma()
    {
        isEquipping = true;
        isAiming = false;
        if (miraLaser != null) miraLaser.enabled = false;
        smoothedMoveInput = Vector3.zero;

        if (!isArmed)
        {
            if (anim != null) anim.SetInteger(hashTipoArma, armaAtual);
            if (anim != null) anim.SetTrigger(hashEquip);
            yield return new WaitForSeconds(momentoDePegarArma);

            isArmed = true;
            if (armaAtual == 1) { macheteNaMao.SetActive(true); macheteNasCostas.SetActive(false); }
            else if (armaAtual == 2) { pistolaNaMao.SetActive(true); pistolaNoColdre.SetActive(false); }
        }
        else
        {
            if (anim != null) anim.SetInteger(hashTipoArma, 0);
            if (anim != null) anim.SetTrigger(hashEquip);
            yield return new WaitForSeconds(momentoDePegarArma);

            isArmed = false;
            DesligarTodasAsArmas();
        }

        yield return new WaitForSeconds(tempoEquipar - momentoDePegarArma);
        isEquipping = false;
    }
    #endregion

    #region Lógica de Combate e Mira Laser
    private void ProcessarInputsDeCombate()
    {
        if (estaEsquivando || isEquipping || !isArmed || !isGrounded())
        {
            isAiming = false;
            if (miraLaser != null) miraLaser.enabled = false;
            return;
        }

        if (armaAtual == 2)
        {
            if (Input.GetMouseButton(1) && !isAttacking)
            {
                isAiming = true;
                AtualizarMiraLaser();
                CalcularRotacaoMouse(true);
            }
            else
            {
                isAiming = false;
                if (miraLaser != null) miraLaser.enabled = false;
            }

            if (Input.GetMouseButtonDown(0) && !isAttacking)
            {
                StartCoroutine(RotinaAtirarPistola());
            }
        }
        else if (armaAtual == 1)
        {
            isAiming = false;
            if (miraLaser != null) miraLaser.enabled = false;

            if (!isAttacking)
            {
                if (Input.GetMouseButtonDown(0)) StartCoroutine(RotinaAtaqueMelee(true));
                else if (Input.GetMouseButtonDown(1)) StartCoroutine(RotinaAtaqueMelee(false));
            }
        }
    }

    private void AtualizarMiraLaser()
    {
        if (miraLaser == null || pontoDeDisparo == null) return;

        miraLaser.enabled = true;
        miraLaser.SetPosition(0, pontoDeDisparo.position);

        LayerMask mascaraLaser = layerInimigos | layerObstaculo | layerChao;

        if (Physics.Raycast(pontoDeDisparo.position, pontoDeDisparo.forward, out RaycastHit hit, alcanceTiro, mascaraLaser))
        {
            miraLaser.SetPosition(1, hit.point);
        }
        else
        {
            miraLaser.SetPosition(1, pontoDeDisparo.position + pontoDeDisparo.forward * alcanceTiro);
        }
    }

    private IEnumerator RotinaAtirarPistola()
    {
        isAttacking = true;
        smoothedMoveInput = Vector3.zero;
        CalcularRotacaoMouse(true);

        if (anim != null) anim.SetTrigger(hashAtirar);
        if (efeitoFogoPistola != null) efeitoFogoPistola.Play();

        if (pontoDeDisparo != null)
        {
            LayerMask mascaraTiro = layerInimigos | layerObstaculo;
            if (Physics.Raycast(pontoDeDisparo.position, pontoDeDisparo.forward, out RaycastHit hit, alcanceTiro, mascaraTiro))
            {
                ZumbiIA zumbi = hit.collider.GetComponent<ZumbiIA>();
                if (zumbi != null)
                {
                    zumbi.ReceberDano(danoPistola, transform.position);
                    StartCoroutine(HitStop());

                    if (efeitoSanguePrefab != null)
                    {
                        Instantiate(efeitoSanguePrefab, hit.point, Quaternion.LookRotation(hit.normal));
                    }
                }
            }
        }

        StartCoroutine(CameraShake());

        yield return new WaitForSeconds(tempoEntreTiros);
        isAttacking = false;
    }

    private IEnumerator RotinaAtaqueMelee(bool isLightAttack)
    {
        isAttacking = true;
        smoothedMoveInput = Vector3.zero;
        CalcularRotacaoMouse(true);

        if (isLightAttack)
        {
            if (anim != null) { anim.SetInteger(hashLightAttackIndex, lightComboIndex); anim.SetTrigger(hashLightAttack); }
            lightComboIndex++;
            if (lightComboIndex > 1) lightComboIndex = 0;

            yield return new WaitForSeconds(tempoAtaqueLeve * porcentagemHitLeve);
            CausarDanoMelee(danoAtaqueLeve);
            yield return new WaitForSeconds(tempoAtaqueLeve * (1f - porcentagemHitLeve));
        }
        else
        {
            if (anim != null) { anim.SetInteger(hashHeavyAttackIndex, heavyComboIndex); anim.SetTrigger(hashHeavyAttack); }
            heavyComboIndex++;
            if (heavyComboIndex > 1) heavyComboIndex = 0;

            yield return new WaitForSeconds(tempoAtaquePesado * porcentagemHitPesado);
            CausarDanoMelee(danoAtaquePesado);
            yield return new WaitForSeconds(tempoAtaquePesado * (1f - porcentagemHitPesado));
        }

        isAttacking = false;
    }

    private void CausarDanoMelee(int dano)
    {
        if (pontoDeAtaqueMelee == null) return;
        Collider[] inimigosAcertados = Physics.OverlapSphere(pontoDeAtaqueMelee.position, raioDoAtaque, layerInimigos);
        bool acertouAlguem = false;

        foreach (Collider inimigo in inimigosAcertados)
        {
            ZumbiIA zumbi = inimigo.GetComponent<ZumbiIA>();
            if (zumbi != null)
            {
                zumbi.ReceberDano(dano, transform.position);
                acertouAlguem = true;

                if (efeitoSanguePrefab != null)
                {
                    Vector3 pontoImpacto = inimigo.ClosestPoint(pontoDeAtaqueMelee.position);
                    Vector3 direcaoSangue = (pontoImpacto - transform.position).normalized;
                    Instantiate(efeitoSanguePrefab, pontoImpacto, Quaternion.LookRotation(direcaoSangue));
                }
            }
        }

        if (acertouAlguem)
        {
            StartCoroutine(HitStop());
            StartCoroutine(CameraShake());
        }
    }

    private IEnumerator HitStop() { Time.timeScale = 0.1f; yield return new WaitForSecondsRealtime(0.04f); Time.timeScale = 1f; }

    private IEnumerator CameraShake()
    {
        float tempo = 0f;
        while (tempo < tempoCameraShake)
        {
            cameraShakeOffset = new Vector3(Random.Range(-1f, 1f) * forcaCameraShake, 0, Random.Range(-1f, 1f) * forcaCameraShake);
            tempo += Time.unscaledDeltaTime;
            yield return null;
        }
        cameraShakeOffset = Vector3.zero;
    }
    #endregion

    #region Movimentação, Câmera e Inputs Gerais
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

    private void CalcularMovimentoFisico()
    {
        if (estaEsquivando) return;

        if (isAttacking || isEquipping)
        {
            currentSpeed = 0f;
            smoothedMoveInput = Vector3.zero;
            return;
        }

        Vector3 targetMoveInput = moveInput.normalized;
        float taxaFrenagemAtual = frenagemCaminhada;
        if (targetMoveInput.magnitude == 0 && currentSpeed > walkSpeed + 0.5f) taxaFrenagemAtual = frenagemCorrida;

        smoothedMoveInput = Vector3.MoveTowards(smoothedMoveInput, targetMoveInput, taxaFrenagemAtual * Time.deltaTime);

        float targetSpeed = walkSpeed;

        if (isAiming && !isCrouching && !isProne) targetSpeed = walkSpeed;
        else if (isProne) targetSpeed = proneSpeed;
        else if (isCrouching) targetSpeed = crouchSpeed;
        else if (querCorrer) targetSpeed = sprintSpeed;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, aceleracaoCorrida * Time.deltaTime);
        if (mudandoPostura) currentSpeed = Mathf.MoveTowards(currentSpeed, proneSpeed, 30f * Time.deltaTime);
    }

    private IEnumerator ExecutarRolamento()
    {
        estaEsquivando = true;
        isCrouching = false;
        isProne = false;
        isAiming = false;

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
            if (progresso <= 0.4f) currentSpeed = velocidadeRolamento;
            else currentSpeed = Mathf.Lerp(velocidadeRolamento, querCorrer ? sprintSpeed : walkSpeed, (progresso - 0.4f) / 0.6f);

            tempoDecorrido += Time.deltaTime;
            yield return null;
        }
        currentSpeed = querCorrer ? sprintSpeed : walkSpeed;
        estaEsquivando = false;
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

        visualCorrida = (querCorrer || (currentSpeed > walkSpeed + 0.5f && smoothedMoveInput.magnitude > 0.05f)) && !isCrouching && !isProne && !isAttacking && !isEquipping && !isAiming;

        anim.SetBool(hashIsCrouching, isCrouching);
        anim.SetBool(hashIsProne, isProne);
        anim.SetBool(hashIsSprinting, visualCorrida);
        anim.SetBool(hashIsArmed, isArmed);
        anim.SetBool(hashIsAiming, isAiming);

        tempoNoAr = !isGrounded() ? tempoNoAr + Time.deltaTime : 0f;
        anim.SetBool(hashIsFalling, (tempoNoAr > 0.30f) && !isProne && !estaEsquivando && !mudandoPostura && Time.time > tempoBloqueioQueda);

        Vector3 localMove = transform.InverseTransformDirection(smoothedMoveInput);
        float alvoMultiplicador = visualCorrida ? 2f : 1f;
        currentAnimMultiplier = Mathf.MoveTowards(currentAnimMultiplier, alvoMultiplicador, aceleracaoCorrida * 0.5f * Time.deltaTime);

        float finalX = localMove.x * currentAnimMultiplier;
        float finalZ = localMove.z * currentAnimMultiplier;

        // A limitação de eixo em 'isProne' foi completamente removida daqui para ele engatinhar para trás na mesma hora.

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
        if (playerCamera != null) { distanciaAtual = offsetCamera.magnitude; distanciaAlvo = distanciaAtual; direcaoOriginalDaCamera = offsetCamera.normalized; }
    }

    private void HandleZoomInput()
    {
        float inputDeZoom = 0f;
        if (usarScrollDoMouse) inputDeZoom = Input.GetAxis("Mouse ScrollWheel") * -1f * sensibilidadeZoom * 10f;
        if (Input.GetKey(botaoZoomIn)) inputDeZoom = -sensibilidadeZoom * Time.deltaTime;
        else if (Input.GetKey(botaoZoomOut)) inputDeZoom = sensibilidadeZoom * Time.deltaTime;
        if (inputDeZoom != 0) { distanciaAlvo += inputDeZoom; distanciaAlvo = Mathf.Clamp(distanciaAlvo, distanciaMinima, distanciaMaxima); }
    }

    private void AplicarZoomECameraFollow()
    {
        distanciaAtual = Mathf.Lerp(distanciaAtual, distanciaAlvo, suavidadeZoom * Time.deltaTime);
        Vector3 novaPosicaoCamera = transform.position + (direcaoOriginalDaCamera * distanciaAtual) + cameraShakeOffset;
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

    void OnDrawGizmosSelected()
    {
        if (pontoDeAtaqueMelee == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pontoDeAtaqueMelee.position, raioDoAtaque);
    }
    private bool isGrounded()
    {
        if (capsuleCollider == null) return false;
        float baseDaCapsula = capsuleCollider.bounds.min.y;
        Vector3 sensor = new Vector3(capsuleCollider.bounds.center.x, baseDaCapsula, capsuleCollider.bounds.center.z) + (Vector3.up * 0.1f);
        return Physics.CheckSphere(sensor, 0.3f, layerChao | layerObstaculo, QueryTriggerInteraction.Ignore);
    }
    #endregion
}