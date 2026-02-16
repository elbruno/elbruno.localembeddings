# Sample Images

This directory is for storing images to search with the CLIP model.

## Getting Sample Images

To test the ImageSearchSample, you need a collection of images. Here are some options:

### Option 1: Use Your Own Images

Copy any images you have to this directory:

```bash
cp ~/Pictures/*.jpg samples/ImageSearchSample/images/
```

Supported formats: `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`

### Option 2: Download Test Datasets

**COCO Validation Set (small subset):**

```bash
# Download a few sample images from COCO
mkdir -p samples/ImageSearchSample/images
cd samples/ImageSearchSample/images

# Example: download some sample images (requires wget or curl)
wget http://images.cocodataset.org/val2017/000000039769.jpg  # cat
wget http://images.cocodataset.org/val2017/000000000139.jpg  # dog
wget http://images.cocodataset.org/val2017/000000000285.jpg  # bicycle
```

**Unsplash Sample Images:**

Download free high-quality images from [Unsplash](https://unsplash.com/):
- Search for topics: "nature", "animals", "cities", "food", etc.
- Download a variety of images (aim for 10-20 images for a good demo)
- Place them in this directory

### Option 3: Public Domain Images

Use public domain images from:
- [Wikimedia Commons](https://commons.wikimedia.org/)
- [Pexels](https://www.pexels.com/)
- [Pixabay](https://pixabay.com/)

## Directory Structure

After adding images, your directory should look like this:

```
samples/ImageSearchSample/images/
├── README.md (this file)
├── cat.jpg
├── dog.jpg
├── bicycle.jpg
├── sunset.jpg
├── mountains.jpg
└── ... (more images)
```

## Important Notes

- **Do NOT commit large images to the repository.** Images are excluded via `.gitignore`.
- For testing, 5-20 images is sufficient to demonstrate the search functionality.
- Use diverse images for better demonstration of semantic search capabilities.
- Image file size doesn't matter — they'll be resized to 224×224 for CLIP encoding.

## Example Queries to Try

Once you have a variety of images, try these queries:

- **Animals:** "a cat", "a dog", "a bird"
- **Nature:** "sunset", "mountains", "ocean", "forest"
- **Objects:** "a car", "a bicycle", "a phone"
- **Activities:** "a person running", "someone reading a book"
- **Scenes:** "a city street", "a beach", "indoors"

The more diverse your image collection, the more interesting the search results!
