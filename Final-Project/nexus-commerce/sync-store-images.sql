SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- Update product images
UPDATE dbo.Products
SET ImageUrl = REPLACE(ImageUrl, '.svg', '.png')
WHERE ImageUrl LIKE '/images/products/p[0-9][0-9].svg';

-- Update category images
UPDATE dbo.Categories
SET ImageUrl = REPLACE(ImageUrl, '.svg', '.png')
WHERE ImageUrl IN (
    '/images/categories/phones-tablets.svg',
    '/images/categories/computers.svg',
    '/images/categories/audio.svg',
    '/images/categories/smart-home.svg',
    '/images/categories/fashion.svg',
    '/images/categories/home-kitchen.svg',
    '/images/categories/sports-outdoors.svg',
    '/images/categories/beauty-care.svg'
);

-- Update product galleries
UPDATE dbo.Products
SET GalleryJson = CONCAT(
    CHAR(91), CHAR(34), ImageUrl, CHAR(34), CHAR(93)
)
WHERE ImageUrl LIKE '/images/products/p[0-9][0-9].png'
  AND ISJSON(GalleryJson) = 1
  AND GalleryJson LIKE '%.svg%';

COMMIT TRANSACTION;

SELECT 'Products' AS Item, COUNT(*) AS PngImages
FROM dbo.Products WHERE ImageUrl LIKE '%.png'
UNION ALL
SELECT 'Categories', COUNT(*)
FROM dbo.Categories WHERE ImageUrl LIKE '%.png';
