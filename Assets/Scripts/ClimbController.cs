using UnityEngine;
using System.Collections;

public class ClimbController : MonoBehaviour
{
    [Header("Configurações de Detecção")]
    public float distanciaDeteccao = 1.0f;
    public float alturaMaximaObstaculo = 3.0f;
    public float alturaMuroBaixo = 1.5f;
    public float alturaMinimaQueda = 0.5f;
    public LayerMask layerObstaculo;

    [Header("Tempo das Animações (em segundos)")]
    public float tempoAnimacaoAlta = 2.5f;
    public float tempoAnimacaoBaixa = 1.5f;
    public float tempoAnimacaoDescerAlta = 2.0f;
    public float tempoAnimacaoDescerBaixa = 1.2f;

    [Header("Referências")]
    public MonoBehaviour scriptDeMovimento;

    private Animator animator;
    private CapsuleCollider capsulaColisao;
    private Rigidbody corpoRigido;
    private bool estaEscalando = false;

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        capsulaColisao = GetComponent<CapsuleCollider>();
        corpoRigido = GetComponent<Rigidbody>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C) && !estaEscalando)
        {
            if (!TentarEscalar())
            {
                TentarDescer();
            }
        }
    }

    private bool TentarEscalar()
    {
        Vector3 origemFrente = transform.position + Vector3.up * 1.0f;

        if (Physics.Raycast(origemFrente, transform.forward, out RaycastHit hitFrente, distanciaDeteccao, layerObstaculo))
        {
            // O raio de cima vai atirar 10cm para frente da beirada
            Vector3 origemCima = hitFrente.point + Vector3.up * alturaMaximaObstaculo + transform.forward * 0.1f;

            if (Physics.Raycast(origemCima, Vector3.down, out RaycastHit hitTopo, alturaMaximaObstaculo, layerObstaculo))
            {
                float alturaDoMuro = hitTopo.point.y - transform.position.y;

                // Posição cravada onde o raio bateu! Ele não vai varar o bloco pequeno e nem flutuar
                Vector3 pontoDeAterrissagem = hitTopo.point + transform.forward * 0.1f;

                if (alturaDoMuro > alturaMuroBaixo)
                {
                    StartCoroutine(RotinaDeEscalada("Subir_Alto_Normal", tempoAnimacaoAlta, pontoDeAterrissagem));
                }
                else
                {
                    StartCoroutine(RotinaDeEscalada("Subir_Baixo", tempoAnimacaoBaixa, pontoDeAterrissagem));
                }
                return true;
            }
        }
        return false;
    }

    private bool TentarDescer()
    {
        // Raio ajustado para 60cm a frente
        Vector3 origemBeirada = transform.position + transform.forward * 0.6f + Vector3.up * 0.1f;

        if (Physics.Raycast(origemBeirada, Vector3.down, out RaycastHit hitChao, alturaMaximaObstaculo + 2f, layerObstaculo))
        {
            float alturaDaQueda = transform.position.y - hitChao.point.y;

            if (alturaDaQueda > alturaMinimaQueda)
            {
                Vector3 pontoDeAterrissagem = hitChao.point;

                if (alturaDaQueda > alturaMuroBaixo)
                {
                    StartCoroutine(RotinaDeEscalada("Descer_Alto", tempoAnimacaoDescerAlta, pontoDeAterrissagem));
                }
                else
                {
                    StartCoroutine(RotinaDeEscalada("Descer_Baixo", tempoAnimacaoDescerBaixa, pontoDeAterrissagem));
                }
                return true;
            }
        }
        return false;
    }

    private IEnumerator RotinaDeEscalada(string nomeAnimacao, float tempoEspera, Vector3 posicaoFinal)
    {
        estaEscalando = true;

        if (scriptDeMovimento != null) scriptDeMovimento.enabled = false;
        if (capsulaColisao != null) capsulaColisao.enabled = false;

        if (corpoRigido != null)
        {
            // Zera a inércia ANTES de desligar a física para evitar o Erro Amarelo
            corpoRigido.linearVelocity = Vector3.zero;
            corpoRigido.isKinematic = true;
            corpoRigido.useGravity = false;
        }

        animator.applyRootMotion = true;
        animator.CrossFade(nomeAnimacao, 0.2f);

        yield return new WaitForSeconds(tempoEspera);

        // Teleporta com exatos 5 centímetros de folga vertical para a cápsula não raspar no chão
        transform.position = posicaoFinal + (Vector3.up * 0.05f);

        animator.transform.localPosition = Vector3.zero;
        animator.applyRootMotion = false;

        if (scriptDeMovimento != null) scriptDeMovimento.enabled = true;
        if (capsulaColisao != null) capsulaColisao.enabled = true;

        if (corpoRigido != null)
        {
            corpoRigido.isKinematic = false;
            corpoRigido.useGravity = true;
            corpoRigido.linearVelocity = Vector3.zero;
        }

        estaEscalando = false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.0f, transform.forward * distanciaDeteccao);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + transform.forward * 0.6f + Vector3.up * 0.1f, Vector3.down * (alturaMaximaObstaculo + 2f));
    }
}