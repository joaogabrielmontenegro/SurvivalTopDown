using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Velocidade do nosso boneco
    public float moveSpeed = 5f;

    void Update()
    {
        // Captura comandos do teclado (WASD / Setas)
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        // Cria o vetor de movimento no plano do chão (X e Z)
        Vector3 direction = new Vector3(moveX, 0f, moveZ).normalized;

        // Move o personagem pelo mundo
        transform.Translate(direction * moveSpeed * Time.deltaTime, Space.World);

        // Se estiver se movendo, vira o corpo para a direção do movimento
        if (direction != Vector3.zero)
        {
            transform.forward = direction;
        }
    }
}