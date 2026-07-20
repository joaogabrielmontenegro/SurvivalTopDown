using System.Collections;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    // 1. Aqui vamos arrastar o nosso Taco para o código saber quem ele deve girar
    public Transform weaponTransform;

    // Tempo total que o golpe vai durar (quanto menor, mais rápido é o golpe)
    public float attackDuration = 0.15f;

    // Variável para controlar se o jogador já está batendo (evita spam de clique)
    private bool isAttacking = false;
    private Quaternion originalRotation;

    void Start()
    {
        // Guarda a rotação padrão (90, 0, 0) que configuramos na Unity
        if (weaponTransform != null)
        {
            originalRotation = weaponTransform.localRotation;
        }
    }

    void Update()
    {
        // Detecta o clique do botão esquerdo do mouse
        if (Input.GetMouseButtonDown(0) && !isAttacking)
        {
            // ISSO VAI MOSTRAR UMA MENSAGEM NO CONSOLE DA UNITY
            Debug.Log("O código ouviu o clique do mouse!");

            StartCoroutine(SwingWeapon());
        }
    }

    // 3. A Corrotina que faz a mágica do movimento acontecer
    IEnumerator SwingWeapon()
    {
        isAttacking = true;

        // Define até onde o taco vai girar para frente no golpe
        // LINHA NOVA (Vai dar a bastonada de lado):
        Quaternion targetRotation = originalRotation * Quaternion.Euler(0f, 0f, 70f);

        // FASE 1: Gira o taco para frente
        float timeElapsed = 0f;
        while (timeElapsed < attackDuration / 2)
        {
            weaponTransform.localRotation = Quaternion.Slerp(originalRotation, targetRotation, timeElapsed / (attackDuration / 2));
            timeElapsed += Time.deltaTime;
            yield return null; // Espera o próximo frame do jogo
        }

        // FASE 2: Traz o taco de volta para a posição original
        timeElapsed = 0f;
        while (timeElapsed < attackDuration / 2)
        {
            weaponTransform.localRotation = Quaternion.Slerp(targetRotation, originalRotation, timeElapsed / (attackDuration / 2));
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        // Garante que o taco voltou exatamente para o lugar e libera o próximo ataque
        weaponTransform.localRotation = originalRotation;
        isAttacking = false;
    }
}