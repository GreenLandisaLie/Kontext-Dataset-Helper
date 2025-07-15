import os
from PIL import Image

try:
    # Try to import Resampling from Pillow 9.1.0+
    from PIL.Image import Resampling
    LANCZOS_RESAMPLING = Resampling.LANCZOS
except ImportError:
    # Fallback for older Pillow versions (pre 9.1.0)
    LANCZOS_RESAMPLING = Image.LANCZOS


script_location = os.path.abspath(__file__)
base_dir = os.path.join(os.path.dirname(script_location), 'base')
ref_dir = os.path.join(os.path.dirname(script_location), 'ref')

MAX_DIM = 2048  # WPF CopyPixels limit
# Note: For conversion to PNG, these will be handled by the convert_to_png_if_needed function
SUPPORTED_FORMATS = ('.png', '.jpg', '.jpeg', '.webp', '.bmp', '.gif')
# Formats that should always be converted to PNG
CONVERT_TO_PNG_FORMATS = ('.bmp', '.gif', '.webp')

def convert_to_png_if_needed(filepath):
    """
    Converts .bmp, static .gif and .webp images to .png format in place.
    If the file is already a PNG or another supported format, it does nothing.
    """
    file_extension = os.path.splitext(filepath)[1].lower()

    if file_extension in CONVERT_TO_PNG_FORMATS:
        try:
            with Image.open(filepath) as img:
                # Construct the new PNG filepath
                new_filepath = os.path.splitext(filepath)[0] + '.png'
                
                # Check if the image has an alpha channel, if not, convert to RGB
                # This helps with potential issues saving certain BMP/GIFs directly to PNG with transparency
                if img.mode in ('P', 'PA'): # Palette-based images or images with alpha in palette
                    img = img.convert("RGBA")
                elif img.mode not in ('RGB', 'RGBA'):
                    img = img.convert("RGB")

                img.save(new_filepath, "PNG")
                print(f"Converted {os.path.basename(filepath)} to PNG: {os.path.basename(new_filepath)}")
                
                # Remove the original file if conversion was successful
                if os.path.exists(new_filepath):
                    os.remove(filepath)
                    print(f"Removed original file: {os.path.basename(filepath)}")

        except Exception as e:
            print(f"Failed to convert {os.path.basename(filepath)} to PNG: {e}")
            return False # Indicate conversion failed
    return True # Indicate no conversion was needed or it was successful

def downscale_image_if_needed(filepath):
    """
    Downscales an image if its dimensions exceed MAX_DIM.
    """
    try:
        with Image.open(filepath) as img:
            width, height = img.size
            if width <= MAX_DIM and height <= MAX_DIM:
                return  # No resizing needed

            # Calculate new size preserving aspect ratio
            scale = min(MAX_DIM / width, MAX_DIM / height)
            new_size = (int(width * scale), int(height * scale))

            print(f"Resizing {os.path.basename(filepath)} from ({width}, {height}) to {new_size}")
            resized = img.resize(new_size, LANCZOS_RESAMPLING)

            # Overwrite with original format. If the image was converted to PNG, it will save as PNG.
            resized.save(filepath, format=img.format)
    except Exception as e:
        print(f"Failed to downscale {os.path.basename(filepath)}: {e}")

def fix_unmatched_pairs_if_needed(base_img_path):
    """
    Resizes images in base_dir or ref_dir if their resolutions don't match.
    Prioritizes making the larger image match the smaller one.
    """
    base_filename = os.path.basename(base_img_path)
    ref_img_path = os.path.join(ref_dir, base_filename) # Assumes same filename in ref_dir

    # Adjust ref_img_path if base_img_path was converted from .bmp/.gif/.webp to .png
    if base_filename.lower().endswith(CONVERT_TO_PNG_FORMATS):
        # Check if a .png version of the base image exists in base_dir
        png_base_img_path = os.path.splitext(base_img_path)[0] + '.png'
        if os.path.exists(png_base_img_path):
            base_img_path = png_base_img_path
            # Also adjust ref_img_path to look for PNG version
            ref_img_path = os.path.join(ref_dir, os.path.splitext(base_filename)[0] + '.png')
        else:
            # If the original non-PNG base file still exists (conversion failed or not needed)
            # and no PNG counterpart, then proceed with the original name
            pass # ref_img_path remains unchanged

    # Handle cases where ref image might have been converted to PNG
    # This check ensures we look for the PNG version of the ref image if the original was BMP/GIF/WEBP
    potential_ref_png_path = os.path.splitext(ref_img_path)[0] + '.png'
    if not os.path.isfile(ref_img_path) and os.path.isfile(potential_ref_png_path):
        ref_img_path = potential_ref_png_path

    if not os.path.isfile(ref_img_path):
        print(f"[WARNING] Could not find reference image for {os.path.basename(base_img_path)}: {ref_img_path}")
        return

    try:
        with Image.open(base_img_path) as base_img, Image.open(ref_img_path) as ref_img:
            base_img_width, base_img_height = base_img.size
            ref_img_width, ref_img_height = ref_img.size

            base_img_res = base_img_width * base_img_height
            ref_img_res = ref_img_width * ref_img_height

            if base_img_res != ref_img_res:
                if base_img_res > ref_img_res:
                    new_size = (ref_img_width, ref_img_height)
                    print(f"Resizing (base) {os.path.basename(base_img_path)} from ({base_img_width}, {base_img_height}) to {new_size} | ref: ({ref_img_width}, {ref_img_height})")
                    resized = base_img.resize(new_size, LANCZOS_RESAMPLING)
                    # Save with its current format (which could be PNG if converted earlier)
                    resized.save(base_img_path, format=base_img.format)
                else:
                    new_size = (base_img_width, base_img_height)
                    print(f"Resizing (ref) {os.path.basename(ref_img_path)} from ({ref_img_width}, {ref_img_height}) to {new_size} | base: ({base_img_width}, {base_img_height})")
                    resized = ref_img.resize(new_size, LANCZOS_RESAMPLING)
                    # Save with its current format (which could be PNG if converted earlier)
                    resized.save(ref_img_path, format=ref_img.format)
    except Exception as e:
        print(f"Failed to fix unmatched pair for {os.path.basename(base_img_path)}: {e}")

def process_directory(dir_path, fix_unmatched_pairs=False):
    """
    Iterates through a directory, applying image processing functions.
    """
    for filename in os.listdir(dir_path):
        full_path = os.path.join(dir_path, filename)
        
        # Skip directories
        if os.path.isdir(full_path):
            continue

        file_extension = os.path.splitext(filename)[1].lower()

        # First, handle conversion to PNG for .bmp, .gif and .webp
        if file_extension in CONVERT_TO_PNG_FORMATS:
            # If conversion happens, the file extension might change, so we need to update full_path
            # The convert_to_png_if_needed function handles removing the old file.
            convert_success = convert_to_png_if_needed(full_path)
            if convert_success:
                full_path = os.path.splitext(full_path)[0] + '.png' # Update path for subsequent operations
                file_extension = '.png' # Update extension
            else:
                continue # Skip if conversion failed

        # Only process if the file is now a supported format (including newly converted PNGs)
        if file_extension in SUPPORTED_FORMATS:
            try:
                if not fix_unmatched_pairs:
                    downscale_image_if_needed(full_path)
                else:
                    # fix_unmatched_pairs_if_needed is designed to work with base_dir paths
                    # so only call it when processing base_dir in the main function's second pass
                    if dir_path == base_dir:
                        fix_unmatched_pairs_if_needed(full_path)
            except Exception as e:
                print(f"Failed to process {os.path.basename(full_path)}: {e}")

def main():
    """
    Main function to orchestrate image processing.
    """
    print("Starting image processing...")

    print("\n--- Converting BMP/GIF/WEBP to PNG and Downscaling (if necessary) in base directory ---")
    process_directory(base_dir)

    print("\n--- Converting BMP/GIF/WEBP to PNG and Downscaling (if necessary) in ref directory ---")
    process_directory(ref_dir)

    print("\n--- Fixing unmatched resolution pairs ---")
    # This pass will now operate on the potentially converted .png files
    process_directory(base_dir, fix_unmatched_pairs=True)

    print("\n✅ Done. All specified image conversions and adjustments completed.")

if __name__ == "__main__":
    main()
