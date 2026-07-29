using System.Collections;
using UnityEngine;

public class PiscaPoste : MonoBehaviour
{
    // Encapsulando a referência da luz do Unity
    private Light luzDoPoste;

    // Variáveis públicas para você controlar no Inspector do Unity
    public float tempoMinimo = 0.05f;
    public float tempoMaximo = 0.5f;

    void Start()
    {
        // Instancia a luz do próprio objeto onde o script for colocado
        luzDoPoste = GetComponent<Light>();

        // Inicia a rotina paralela de tempo
        StartCoroutine(EfeitoPiscar());
    }

    IEnumerator EfeitoPiscar()
    {
        while (true) // Loop infinito para a luz continuar piscando sempre
        {
            // Sorteia um tempo aleatório entre o mínimo e o máximo
            float tempoDeEspera = Random.Range(tempoMinimo, tempoMaximo);

            // Pausa a execução por esse tempo
            yield return new WaitForSeconds(tempoDeEspera);

            // Inverte o estado lógico da luz (se for true vira false, e vice-versa)
            luzDoPoste.enabled = !luzDoPoste.enabled;
        }
    }
}