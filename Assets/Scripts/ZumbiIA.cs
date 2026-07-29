using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class ZumbiIA : MonoBehaviour
{
    [Header("Sistema de Furtividade (Stealth)")]
    [Range(0, 360)] public float anguloDeVisao = 90f;
    public LayerMask layerObstaculos;

    [Header("Investigação e Fuga")]
    [SerializeField] private float tempoDeTeimosia = 3.5f;
    [SerializeField] private float tempoDeBusca = 6f;

    [Header("Alimentação (Corpos)")]
    public LayerMask layerCorpos;
    public float raioBuscaCorpos = 15f;

    [Header("Ataque e Grito")]
    [SerializeField] private float distanciaDeAtaque = 1.5f;
    [SerializeField] private float tempoEntreAtaques = 1.5f;
    [SerializeField] private float tempoDaAnimacaoAtaque = 1.0f;
    [SerializeField] private int chanceDeGritar = 30;
    [SerializeField] private float tempoDoGrito = 2.5f;

    [Header("Patrulha (Roam)")]
    [SerializeField] private float raioDePatrulha = 15f;
    [SerializeField] private float tempoMinimoParado = 15f;
    [SerializeField] private float tempoMaximoParado = 30f;
    [SerializeField] private float tempoMinimoAndando = 4f;
    [SerializeField] private float tempoMaximoAndando = 7f;
    [SerializeField] private float velocidadePatrulha = 0.7f;

    [Header("Vida")]
    public int vidaAtual = 100;
    private bool estaMorto = false;
    private bool estaLevandoDano = false;

    [Header("Referências")]
    [SerializeField] private Transform jogador;

    private NavMeshAgent agente;
    private Animator anim;
    private Animator animJogador;

    // --- CONTROLES DE ESTADO E MEMÓRIA ---
    private bool jaTeViu = false;
    private bool estaGritando = false;
    private bool estaAtacando = false;
    private float momentoDoUltimoAtaque = 0f;
    private float velocidadeNormal;

    private Vector3 ultimaPosicaoConhecida;
    private bool estaInvestigando = false;
    private Coroutine rotinaInvestigacao;
    private float tempoSemVerOJogador = 0f;

    private bool estaPatrulhando = false;
    private float cronometroParado = 0f;
    private float cronometroAndando = 0f;

    private bool estaComendo = false;
    private Coroutine rotinaComer;
    private Transform corpoAlvo;
    private List<Transform> corposIgnorados = new List<Transform>();
    private int maxRefeicoesNoCorpo = 1;
    private int refeicoesAtuais = 0;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        velocidadeNormal = agente.speed;

        // 🚨 CORREÇÃO DO BUG 2 (Amontoado):
        // Obriga o zumbi a parar de empurrar os outros quando chegar na distância de ataque
        agente.stoppingDistance = distanciaDeAtaque;

        // Dá uma prioridade aleatória para eles desviarem melhor uns dos outros
        agente.avoidancePriority = Random.Range(30, 60);

        if (jogador == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) jogador = player.transform;
        }

        if (jogador != null)
        {
            animJogador = jogador.GetComponentInChildren<Animator>();
        }

        if (anim != null)
        {
            anim.SetFloat("VariacaoIdle", Random.Range(0, 3));
            anim.SetInteger("VariacaoWalk", Random.Range(0, 2));
            anim.SetFloat("VariacaoPatrulha", Random.Range(0, 2));
        }

        cronometroParado = Random.Range(tempoMinimoParado, tempoMaximoParado);
    }

    void Update()
    {
        if (jogador == null || estaMorto || estaLevandoDano) return;

        if (anim != null)
        {
            anim.SetFloat("Velocidade", agente.velocity.magnitude);
            anim.SetBool("Patrulhando", estaPatrulhando);
        }

        float distanciaProJogador = Vector3.Distance(transform.position, jogador.position);
        bool jogadorVisivel = JogadorFoiDetectado(distanciaProJogador);

        if (jogadorVisivel)
        {
            tempoSemVerOJogador = 0f;
        }
        else if (jaTeViu)
        {
            tempoSemVerOJogador += Time.deltaTime;
        }

        bool jogadorDetectado = jogadorVisivel || (jaTeViu && tempoSemVerOJogador <= tempoDeTeimosia);

        if (jogadorDetectado)
        {
            if (estaInvestigando) PararInvestigacao();
            if (estaComendo) InterromperComida();
            if (estaGritando || estaAtacando) return;

            estaPatrulhando = false;
            ultimaPosicaoConhecida = jogador.position;

            if (distanciaProJogador > distanciaDeAtaque)
            {
                if (!jaTeViu)
                {
                    jaTeViu = true;
                    DecidirSeVaiGritar();
                }
                PerseguirAlvo(jogador.position);
            }
            else
            {
                if (Time.time >= momentoDoUltimoAtaque + tempoEntreAtaques)
                {
                    StartCoroutine(RotinaDeAtaque());
                }
                else
                {
                    FicarParado();
                    EncararAlvo(jogador.position);
                }
            }
        }
        else if (jaTeViu)
        {
            if (!estaInvestigando) rotinaInvestigacao = StartCoroutine(RotinaDeInvestigacao());
        }
        else
        {
            jaTeViu = false;

            if (!estaComendo && !estaInvestigando)
            {
                bool achouComida = ProcurarCorpos();
                if (!achouComida) ComportamentoDePatrulha();
            }
        }
    }

    // =======================================================
    // FUNÇÕES DE AÇÃO 
    // =======================================================
    private IEnumerator RotinaDeInvestigacao()
    {
        estaInvestigando = true;
        PerseguirAlvo(ultimaPosicaoConhecida);
        while (Vector3.Distance(transform.position, ultimaPosicaoConhecida) > 2f)
        {
            if (!agente.pathPending && agente.remainingDistance <= agente.stoppingDistance) break;
            yield return null;
        }
        FicarParado();
        yield return new WaitForSeconds(tempoDeBusca);
        estaInvestigando = false;
        jaTeViu = false;
    }

    private void PararInvestigacao()
    {
        if (rotinaInvestigacao != null) StopCoroutine(rotinaInvestigacao);
        estaInvestigando = false;
    }

    private void ComportamentoDePatrulha()
    {
        if (estaPatrulhando)
        {
            cronometroAndando -= Time.deltaTime;
            if (cronometroAndando <= 0f || (!agente.pathPending && agente.remainingDistance <= agente.stoppingDistance))
            {
                estaPatrulhando = false;
                cronometroParado = Random.Range(tempoMinimoParado, tempoMaximoParado);
                FicarParado();
            }
        }
        else
        {
            cronometroParado -= Time.deltaTime;
            if (cronometroParado <= 0f)
            {
                Vector3 pontoAleatorio = transform.position + Random.insideUnitSphere * raioDePatrulha;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(pontoAleatorio, out hit, raioDePatrulha, NavMesh.AllAreas))
                {
                    agente.speed = velocidadePatrulha;
                    agente.isStopped = false;
                    agente.SetDestination(hit.position);
                    estaPatrulhando = true;
                    cronometroAndando = Random.Range(tempoMinimoAndando, tempoMaximoAndando);
                }
            }
        }
    }

    private void PerseguirAlvo(Vector3 alvo)
    {
        agente.speed = velocidadeNormal;
        agente.isStopped = false;
        agente.SetDestination(alvo);
    }

    private void FicarParado()
    {
        agente.isStopped = true;
        agente.velocity = Vector3.zero;
    }

    private void EncararAlvo(Vector3 alvo)
    {
        Vector3 direcao = (alvo - transform.position).normalized;
        direcao.y = 0;
        if (direcao != Vector3.zero)
        {
            Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, Time.deltaTime * 5f);
        }
    }

    private IEnumerator RotinaDeAtaque()
    {
        estaAtacando = true;
        agente.isStopped = true;
        agente.velocity = Vector3.zero;

        if (anim != null)
        {
            anim.SetInteger("VariacaoAtaque", Random.Range(0, 2));
            anim.SetTrigger("Atacar");
        }
        yield return new WaitForSeconds(tempoDaAnimacaoAtaque);
        estaAtacando = false;
        momentoDoUltimoAtaque = Time.time;
    }

    private void DecidirSeVaiGritar()
    {
        int dadoAleatorio = Random.Range(0, 101);
        if (dadoAleatorio <= chanceDeGritar) StartCoroutine(RotinaDeGrito());
    }

    private IEnumerator RotinaDeGrito()
    {
        estaGritando = true;
        agente.isStopped = true;
        agente.velocity = Vector3.zero;
        if (anim != null) anim.SetTrigger("Gritar");
        yield return new WaitForSeconds(tempoDoGrito);
        estaGritando = false;
    }

    private bool ProcurarCorpos()
    {
        if (corpoAlvo == null)
        {
            Collider[] corposProximos = Physics.OverlapSphere(transform.position, raioBuscaCorpos, layerCorpos);
            foreach (Collider col in corposProximos)
            {
                if (!corposIgnorados.Contains(col.transform))
                {
                    corpoAlvo = col.transform;
                    maxRefeicoesNoCorpo = Random.Range(1, 3);
                    refeicoesAtuais = 0;
                    return true;
                }
            }
            return false;
        }
        else
        {
            float distanciaProCorpo = Vector3.Distance(transform.position, corpoAlvo.position);
            if (distanciaProCorpo > distanciaDeAtaque)
            {
                agente.speed = velocidadeNormal;
                agente.isStopped = false;
                agente.SetDestination(corpoAlvo.position);
            }
            else if (rotinaComer == null)
            {
                rotinaComer = StartCoroutine(RotinaDeComer());
            }
            return true;
        }
    }

    private IEnumerator RotinaDeComer()
    {
        estaComendo = true;
        agente.isStopped = true;
        agente.velocity = Vector3.zero;

        if (anim != null) anim.SetBool("ComendoCorpo", true);
        float tempoComendo = Random.Range(12f, 18f);
        yield return new WaitForSeconds(tempoComendo);

        if (anim != null) anim.SetBool("ComendoCorpo", false);
        estaComendo = false;
        rotinaComer = null;
        refeicoesAtuais++;

        if (refeicoesAtuais >= maxRefeicoesNoCorpo)
        {
            corposIgnorados.Add(corpoAlvo);
            corpoAlvo = null;
        }
        cronometroParado = 2f;
    }

    private void InterromperComida()
    {
        if (rotinaComer != null) StopCoroutine(rotinaComer);
        rotinaComer = null;
        estaComendo = false;
        if (anim != null) anim.SetBool("ComendoCorpo", false);
    }

    private bool JogadorFoiDetectado(float distancia)
    {
        float raioAudicao = 8f;
        float raioVisao = 15f;

        if (animJogador != null)
        {
            bool deitado = animJogador.GetBool("isProne");
            bool agachado = animJogador.GetBool("isCrouching");
            bool correndo = animJogador.GetBool("isSprinting");
            float velX = animJogador.GetFloat("VelocityX");
            float velZ = animJogador.GetFloat("VelocityZ");
            bool estaEmMovimento = (Mathf.Abs(velX) + Mathf.Abs(velZ)) > 0.05f;

            if (deitado) { raioAudicao = 0f; raioVisao = 2.5f; }
            else if (agachado) { raioAudicao = estaEmMovimento ? 1f : 0f; raioVisao = 5f; }
            else if (correndo) { raioAudicao = 8f; raioVisao = 13f; }
            else { raioAudicao = estaEmMovimento ? 6f : 0f; raioVisao = 10f; }
        }

        if (distancia <= raioAudicao) return true;

        if (distancia <= raioVisao)
        {
            Vector3 origemOlhos = transform.position + (Vector3.up * 1.5f);
            Vector3 alvoJogador = jogador.position + (Vector3.up * 1.0f);
            Vector3 direcaoProJogador = (alvoJogador - origemOlhos).normalized;
            float anguloComJogador = Vector3.Angle(transform.forward, direcaoProJogador);

            if (anguloComJogador <= anguloDeVisao / 2f)
            {
                float distanciaReal = Vector3.Distance(origemOlhos, alvoJogador);

                // 🚨 CORREÇÃO DO BUG 1 (Invisibilidade ao parar):
                // Agora verificamos exatamente no que o raio da visão bateu.
                RaycastHit hit;
                if (Physics.Raycast(origemOlhos, direcaoProJogador, out hit, distanciaReal, layerObstaculos))
                {
                    // Se bateu no jogador (ou num filho do jogador), ele está visível!
                    if (hit.transform == jogador || hit.transform.IsChildOf(jogador))
                    {
                        return true;
                    }
                    return false; // Bateu em uma parede real
                }
                return true; // Não bateu em nada (Caminho livre)
            }
        }
        return false;
    }

    // =======================================================
    // DANO E MORTE 
    // =======================================================

    public void ReceberDano(int quantidadeDeDano, Vector3 posicaoAtacante)
    {
        if (estaMorto) return;

        vidaAtual -= quantidadeDeDano;
        InterromperComida();

        if (vidaAtual <= 0)
        {
            Morrer();
        }
        else
        {
            StartCoroutine(RotinaLevarDano());
        }
    }

    private IEnumerator RotinaLevarDano()
    {
        estaLevandoDano = true;
        agente.isStopped = true;
        agente.velocity = Vector3.zero;

        if (anim != null)
        {
            anim.SetInteger("VariacaoDano", Random.Range(0, 2));
            anim.SetTrigger("TomarDano");
        }

        yield return new WaitForSeconds(0.8f);

        estaLevandoDano = false;
    }

    public void Morrer()
    {
        if (estaMorto) return;
        estaMorto = true;

        StopAllCoroutines();
        agente.isStopped = true;
        agente.velocity = Vector3.zero;

        if (anim != null)
        {
            anim.SetInteger("VariacaoMorte", Random.Range(0, 2));
            anim.SetTrigger("Morrer");
        }

        agente.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        this.enabled = false;
    }
}