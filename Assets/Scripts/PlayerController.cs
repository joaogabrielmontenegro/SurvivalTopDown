using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Configurações de Movimento")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    public float crouchSpeed = 3.5f;
    public float rotationSpeed = 10f;

    [Header("Ajuste de Postura (Procedural)")]
    [Tooltip("Arraste o osso da coluna (Spine) do seu personagem aqui")]
    public Transform spineBone;
    [Tooltip("O quanto ele inclina para a frente quando está de pé parado (em graus)")]
    public float forwardTiltStanding = 12f;
    [Tooltip("O quanto ele inclina para a frente quando está correndo (em graus)")]
    public float forwardTiltSprinting = 20f;
    public float postureSmoothSpeed = 8f;

    private Rigidbody rb;
    private Vector3 moveInput;
    private Camera mainCamera;
    private Animator anim;
    private float currentSpeed;
    private bool isCrouching = false;

    // Controle interno para suavizar a inclinação
    private float currentTilt = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;
        rb.freezeRotation = true;
        anim = GetComponentInChildren<Animator>();

        currentSpeed = walkSpeed;
    }

    void Update()
    {
        // 1. Captura inputs do teclado
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.z = Input.GetAxisRaw("Vertical");
        moveInput = moveInput.normalized;

        // 2. MÓDULO TOGGLE: Agachamento
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            isCrouching = !isCrouching;
        }

        bool querCorrer = Input.GetKey(KeyCode.LeftShift) && moveInput.magnitude > 0;

        if (querCorrer && isCrouching)
        {
            isCrouching = false;
        }

        // Definição de velocidades
        if (isCrouching) currentSpeed = crouchSpeed;
        else if (querCorrer) currentSpeed = sprintSpeed;
        else currentSpeed = walkSpeed;

        // 3. Lógica de olhar para o mouse
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
        float rayLength;

        if (groundPlane.Raycast(ray, out rayLength))
        {
            Vector3 pointToLook = ray.GetPoint(rayLength);
            Vector3 direction = pointToLook - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 2f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        // 4. Alimenta os dados do Animator
        if (anim != null)
        {
            anim.SetBool("isCrouching", isCrouching);
            anim.SetBool("isSprinting", querCorrer);

            Vector3 localMove = transform.InverseTransformDirection(moveInput);
            float multiplicadorAnimacao = (querCorrer && !isCrouching) ? 2f : 1f;

            anim.SetFloat("VelocityX", localMove.x * multiplicadorAnimacao, 0.05f, Time.deltaTime);
            anim.SetFloat("VelocityZ", localMove.z * multiplicadorAnimacao, 0.05f, Time.deltaTime);
        }
    }

    // O truque acontece aqui: LateUpdate roda DEPOIS das animações
    void LateUpdate()
    {
        if (spineBone != null)
        {
            float targetTilt = 0f;
            bool querCorrer = Input.GetKey(KeyCode.LeftShift) && moveInput.magnitude > 0;

            // Só aplica a inclinação manual se NÃO estiver agachado 
            // (pois a animação nova de agachar que você pegou já tem uma postura ótima por si só)
            if (!isCrouching)
            {
                targetTilt = querCorrer ? forwardTiltSprinting : forwardTiltStanding;
            }

            // Suaviza a transição da postura
            currentTilt = Mathf.Lerp(currentTilt, targetTilt, postureSmoothSpeed * Time.deltaTime);

            // Rotaciona o osso da coluna levemente para a frente no eixo X local
            spineBone.localRotation *= Quaternion.Euler(currentTilt, 0f, 0f);
        }
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + moveInput * currentSpeed * Time.fixedDeltaTime);
    }
}