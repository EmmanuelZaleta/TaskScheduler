#!/usr/bin/env python3
"""
Script para crear un icono básico para YCC Job Host
Requiere Pillow: pip install Pillow
"""

try:
    from PIL import Image, ImageDraw, ImageFont
    import os

    def create_icon(output_path="icon.ico"):
        """Crea un icono simple para la aplicación"""

        # Tamaños estándar de iconos
        sizes = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
        images = []

        for size in sizes:
            # Crear imagen con fondo azul corporativo
            img = Image.new('RGBA', size, (46, 117, 182, 255))  # Color #2E75B6
            draw = ImageDraw.Draw(img)

            # Dibujar un borde redondeado
            border_width = max(1, size[0] // 16)
            draw.rectangle(
                [(border_width, border_width),
                 (size[0] - border_width - 1, size[1] - border_width - 1)],
                outline=(255, 255, 255, 255),
                width=border_width
            )

            # Dibujar letra "Y" en el centro (simplificado como dos líneas)
            center_x, center_y = size[0] // 2, size[1] // 2
            line_width = max(2, size[0] // 8)

            # Brazo izquierdo de la Y
            draw.line(
                [(center_x - size[0] // 4, center_y - size[1] // 4),
                 (center_x, center_y)],
                fill=(255, 255, 255, 255),
                width=line_width
            )

            # Brazo derecho de la Y
            draw.line(
                [(center_x + size[0] // 4, center_y - size[1] // 4),
                 (center_x, center_y)],
                fill=(255, 255, 255, 255),
                width=line_width
            )

            # Tallo de la Y
            draw.line(
                [(center_x, center_y),
                 (center_x, center_y + size[1] // 4)],
                fill=(255, 255, 255, 255),
                width=line_width
            )

            images.append(img)

        # Guardar como ICO con múltiples resoluciones
        images[0].save(
            output_path,
            format='ICO',
            sizes=[img.size for img in images],
            append_images=images[1:]
        )

        print(f"✓ Icono creado exitosamente: {output_path}")
        print(f"  Tamaños incluidos: {', '.join([f'{s[0]}x{s[1]}' for s in sizes])}")
        return True

    if __name__ == "__main__":
        script_dir = os.path.dirname(os.path.abspath(__file__))
        icon_path = os.path.join(script_dir, "icon.ico")

        print("Creando icono para YCC Job Host...")
        create_icon(icon_path)
        print("\nNOTA: Este es un icono básico generado automáticamente.")
        print("Para un aspecto más profesional, reemplace 'icon.ico' con un icono diseñado por un profesional.")

except ImportError:
    print("ERROR: Pillow no está instalado.")
    print("Instale con: pip install Pillow")
    print("\nAlternativamente, puede:")
    print("1. Crear un icono manualmente con una herramienta como GIMP, Photoshop o un generador online")
    print("2. Guardar como 'icon.ico' en este directorio")
    print("3. Ejecutar build.bat para construir el instalador")
    exit(1)
except Exception as e:
    print(f"ERROR: {e}")
    exit(1)
