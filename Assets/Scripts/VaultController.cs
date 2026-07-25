using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class VaultController : MonoBehaviour
{
    [Header("MUITO IMPORTANTE: Animator")]
    [SerializeField] private string nomeEstadoAnimacao = "Vault";
    [Tooltip("Nome EXATO da caixa da animação de pouso lá no Animator")]
    [SerializeField] private string nomeEstadoPouso = "Pousa";

    [Header("Configurações de Detecção")]
    [SerializeField] private float distanciaDeteccao = 2.0f;
    [SerializeField] private float alturaMaximaMuro = 1.4f;
    [SerializeField] private float alturaMinimaMuro = 0.4f;

    [Header("Sincronia do Pulo")]
    [SerializeField] private float recuoInicial = 2.0f;
    [SerializeField] private float tempoVault = 1.9f;
    [SerializeField] private float velocidadeAnimacao = 1.0f;

    [Header("Medidas do Pulo")]
    [SerializeField] private float distanciaPousoAlemDoMuro = 1.0f;
    [SerializeField] private float elevacaoExtra = -0.8f;

    [Header("Recuperação do Pouso")]
    [Tooltip("Tempo (em segundos) que o boneco fica na pose de pouso ANTES de você poder voltar a andar.")]
    [SerializeField] private float tempoRecuperacaoPouso = 0.3f;

    [Header("Camadas")]
    [SerializeField] private LayerMask layerObstaculo;
    [SerializeField] private LayerMask layerChao;

    [Header("Referências")]
    [SerializeField] private MonoBehaviour playerController;
    [SerializeField] private MonoBehaviour playerAnimator;

    private Animator anim;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private bool estaPassandoCerca = false;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        if (playerController == null) playerController = GetComponent("PlayerController") as MonoBehaviour;
        if (playerAnimator == null) playerAnimator = GetComponent("PlayerAnimator") as MonoBehaviour;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C) && !estaPassandoCerca)
        {
            TentarPassarCerca();
        }
    }

    private void TentarPassarCerca()
    {
        float offsetPes = transform.position.y - capsuleCollider.bounds.min.y;
        Vector3 origemCintura = transform.position - (Vector3.up * offsetPes) + (Vector3.up * 0.5f);
        Vector3 origemCabeca = transform.position - (Vector3.up * offsetPes) + (Vector3.up * 1.7f);

        if (Physics.Raycast(origemCabeca, transform.forward, distanciaDeteccao, layerObstaculo, QueryTriggerInteraction.Ignore)) return;
        if (!Physics.Raycast(origemCintura, transform.forward, out RaycastHit hitMuroFrente, distanciaDeteccao, layerObstaculo, QueryTriggerInteraction.Ignore)) return;

        Vector3 origemTopo = hitMuroFrente.point + (transform.forward * 0.1f) + (Vector3.up * 2.0f);
        if (!Physics.Raycast(origemTopo, Vector3.down, out RaycastHit hitTopo, 3.0f, layerObstaculo, QueryTriggerInteraction.Ignore)) return;

        float alturaSolaPe = transform.position.y - offsetPes;
        float alturaMuro = hitTopo.point.y - alturaSolaPe;
        if (alturaMuro < alturaMinimaMuro || alturaMuro > alturaMaximaMuro) return;

        float espessuraMuro = 0.4f;
        Vector3 pontoAtrasDoMuro = hitMuroFrente.point + (transform.forward * 4.0f);
        if (Physics.Raycast(pontoAtrasDoMuro, -transform.forward, out RaycastHit hitMuroTras, 4.0f, layerObstaculo, QueryTriggerInteraction.Ignore))
        {
            espessuraMuro = Vector3.Distance(hitMuroFrente.point, hitMuroTras.point);
        }

        float avancoTotal = espessuraMuro + distanciaPousoAlemDoMuro;
        Vector3 pontoPousoExato = hitMuroFrente.point + (transform.forward * avancoTotal);
        Vector3 origemPousoChao = pontoPousoExato + (Vector3.up * 2.5f);
        LayerMask camadasSuportadas = layerChao | layerObstaculo;

        Vector3 chaoFinal;
        if (Physics.Raycast(origemPousoChao, Vector3.down, out RaycastHit hitPouso, 5.0f, camadasSuportadas, QueryTriggerInteraction.Ignore))
        {
            chaoFinal = hitPouso.point;
        }
        else
        {
            chaoFinal = new Vector3(pontoPousoExato.x, transform.position.y - offsetPes, pontoPousoExato.z);
        }

        Vector3 centroFinal = chaoFinal + (Vector3.up * offsetPes);

        Vector3 posInicialIdeal = hitMuroFrente.point + (hitMuroFrente.normal * recuoInicial);
        posInicialIdeal.y = transform.position.y;

        Vector3 direcaoMuro = -hitMuroFrente.normal;
        direcaoMuro.y = 0;
        if (direcaoMuro != Vector3.zero) transform.rotation = Quaternion.LookRotation(direcaoMuro);

        StartCoroutine(ExecutarVaultFinal(posInicialIdeal, centroFinal, hitTopo.point.y, offsetPes));
    }

    private IEnumerator ExecutarVaultFinal(Vector3 posInicial, Vector3 posFinal, float alturaMuroY, float offsetPes)
    {
        estaPassandoCerca = true;

        transform.position = posInicial;

        if (playerController != null) playerController.enabled = false;
        if (playerAnimator != null) playerAnimator.enabled = false;

        rb.isKinematic = true;
        if (capsuleCollider != null) capsuleCollider.enabled = false;

        float[] pesosCamadasOriginais = null;
        if (anim != null)
        {
            pesosCamadasOriginais = new float[anim.layerCount];
            for (int i = 0; i < anim.layerCount; i++)
            {
                pesosCamadasOriginais[i] = anim.GetLayerWeight(i);
                if (i > 0) anim.SetLayerWeight(i, 0f);
            }

            anim.applyRootMotion = false;
            anim.speed = velocidadeAnimacao;

            anim.Play(nomeEstadoAnimacao, 0, 0f);
        }

        float tempoDecorrido = 0f;
        float picoDoArco = alturaMuroY + offsetPes + elevacaoExtra;
        float forcaDoPuloY = Mathf.Max(0.1f, picoDoArco - Mathf.Max(posInicial.y, posFinal.y));

        while (tempoDecorrido < tempoVault)
        {
            float t = Mathf.Clamp01(tempoDecorrido / tempoVault);
            float tSuave = Mathf.SmoothStep(0f, 1f, t);

            Vector3 posAtual = Vector3.Lerp(posInicial, posFinal, tSuave);
            float baseY = Mathf.Lerp(posInicial.y, posFinal.y, tSuave);
            posAtual.y = baseY + (Mathf.Sin(t * Mathf.PI) * forcaDoPuloY);

            transform.position = posAtual;

            tempoDecorrido += Time.deltaTime;
            yield return null;
        }

        transform.position = posFinal;

        if (anim != null && !string.IsNullOrEmpty(nomeEstadoPouso))
        {
            anim.speed = 1.0f;
            anim.CrossFadeInFixedTime(nomeEstadoPouso, 0.1f);

            yield return new WaitForSeconds(tempoRecuperacaoPouso);
        }

        if (anim != null)
        {
            anim.speed = 1.0f;
            if (pesosCamadasOriginais != null)
            {
                for (int i = 0; i < anim.layerCount; i++) anim.SetLayerWeight(i, pesosCamadasOriginais[i]);
            }
        }

        if (capsuleCollider != null) capsuleCollider.enabled = true;
        rb.isKinematic = false;

        yield return new WaitForFixedUpdate();

        if (playerAnimator != null) playerAnimator.enabled = true;
        if (playerController != null) playerController.enabled = true;
        estaPassandoCerca = false;
    }
}