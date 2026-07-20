using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // Aqui arrastaremos o nosso Player
    private Vector3 offset;   // Vai guardar a distância e o ângulo que você escolheu para a câmera

    void Start()
    {
        if (target != null)
        {
            // Salva a distância exata em que a câmera está do player no início do jogo
            offset = transform.position - target.position;
        }
    }

    // LateUpdate roda logo APÓS o movimento do player, evitando que a câmera trema
    void LateUpdate()
    {
        if (target != null)
        {
            // Mantém a câmera na mesma distância, acompanhando apenas a posição do Player
            transform.position = target.position + offset;
        }
    }
}