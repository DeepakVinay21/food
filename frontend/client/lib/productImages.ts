const img = (id: string) =>
  `https://images.unsplash.com/photo-${id}?auto=format&fit=crop&w=300&q=80`;

// Product name keywords → Unsplash image IDs
const productImages: [string[], string][] = [
  // Dairy
  [["milk"], img("1563636619-e9107da5a165")],
  [["cheese", "paneer"], img("1486297678071-483b86c30514")],
  [["butter", "ghee"], img("1589985270826-4b7bb135bc9d")],
  [["yogurt", "curd", "dahi"], img("1488477181946-6428a0291777")],
  [["cream"], img("1625937286906-d6f83b03f764")],

  // Fruits
  [["apple"], img("1568702846914-96b305d2aaeb")],
  [["banana"], img("1571771894821-ce9b6c11b08e")],
  [["orange", "citrus"], img("1547514701-42782101795e")],
  [["mango"], img("1553279768-865429fa0078")],
  [["grape"], img("1537640538966-79f369143f8f")],
  [["strawberr"], img("1464965911861-746a04b4bca6")],
  [["watermelon"], img("1563114773-84221bd62daa")],
  [["pineapple"], img("1550258987-190a2d41a8ba")],
  [["lemon", "lime"], img("1590502593747-42a996133562")],
  [["pomegranate"], img("1615485290382-441e4d049cb5")],
  [["papaya"], img("1517282009859-f000ec3b26fe")],
  [["coconut"], img("1581074249210-0b347287ee4d")],
  [["guava"], img("1536511132770-e5058c7e8c46")],
  [["fruit"], img("1619566636858-adf3ef46400b")],

  // Vegetables
  [["tomato"], img("1597362925123-77861d3fbac7")],
  [["onion"], img("1508747703725-7197771375a0")],
  [["potato", "aloo"], img("1518977676601-b28d4bbe3772")],
  [["carrot"], img("1598170845058-32b9d6a5da37")],
  [["spinach", "palak"], img("1576045057995-568f588f82fb")],
  [["cabbage"], img("1594282486552-05b4d80fbb9f")],
  [["capsicum", "pepper", "bell pepper"], img("1563565375-f3fdfdbefa83")],
  [["cucumber"], img("1449300079323-02e209d9d3a6")],
  [["broccoli"], img("1459411552884-841db9b3cc2a")],
  [["cauliflower", "gobi"], img("1568702846914-96b305d2aaeb")],
  [["peas", "matar"], img("1563636619-e9107da5a165")],
  [["beans", "rajma"], img("1551462147-37885acc36f1")],
  [["corn"], img("1551754655-cd27e38d2076")],
  [["garlic", "lahsun"], img("1540148426945-6cf22a6b2a28")],
  [["ginger", "adrak"], img("1615485290382-441e4d049cb5")],
  [["mushroom"], img("1504545102780-26c3ba883989")],
  [["lettuce", "salad"], img("1512621776951-a57141f2eefd")],
  [["vegetable", "veggie", "sabji", "sabzi"], img("1540420773420-3366772f4999")],

  // Meat & Protein
  [["chicken"], img("1587593810167-a84920ea0781")],
  [["mutton", "lamb", "goat"], img("1603048297172-c5d25dcb7816")],
  [["fish", "salmon", "tuna"], img("1510130113550-55f6e48a6e56")],
  [["prawn", "shrimp"], img("1565680018093-ebb6436fee6b")],
  [["egg"], img("1506976785307-8732e854ad03")],
  [["meat"], img("1607623814075-e51df1bdc82f")],

  // Bakery
  [["bread", "toast", "loaf"], img("1509440159596-0249088772ff")],
  [["cake"], img("1578985545062-69928b1d9587")],
  [["biscuit", "cookie"], img("1558961363-fa8fdf82db35")],
  [["pastry", "croissant"], img("1555507036-ab1f4038024a")],
  [["muffin", "cupcake"], img("1587668178277-295251f900ce")],
  [["roti", "chapati", "naan", "paratha"], img("1565557623262-b51c2513a641")],

  // Grains & Staples
  [["rice", "chawal", "biryani", "pulao"], img("1536304929831-ee1ca9d44c28")],
  [["wheat", "atta", "flour", "maida"], img("1574323347407-f5e1ad6d020b")],
  [["pasta", "noodle", "spaghetti", "macaroni"], img("1551462147-ff2a5e10ef39")],
  [["oats", "oatmeal", "muesli", "cereal"], img("1517673400267-0251440c45dc")],
  [["dal", "lentil", "moong", "toor", "chana"], img("1585996965946-3e7cc72e00ec")],
  [["poha", "flattened rice"], img("1515003197210-e0cd71810b5f")],

  // Beverages
  [["juice"], img("1621506289937-a8e4df240d0b")],
  [["coffee"], img("1509042239860-f550ce710b93")],
  [["tea", "chai"], img("1556679343-c7306c1976bc")],
  [["soda", "cola", "soft drink"], img("1527960471264-932f39eb5846")],
  [["water"], img("1548839140-29a749e1cf4d")],

  // Condiments & Sauces
  [["ketchup", "sauce", "chutney"], img("1472476443507-c7a5948772fc")],
  [["pickle", "achar"], img("1589621316382-008455b775cd")],
  [["honey"], img("1587049352846-4a222e784d38")],
  [["jam", "jelly", "marmalade"], img("1563805042-7684c019e1cb")],
  [["oil", "olive oil"], img("1474979266404-7eaacbcd87c5")],
  [["salt", "sugar", "spice", "masala"], img("1532336414738-cf97a6c27af2")],

  // Snacks
  [["chips", "crisps", "wafer"], img("1566478989037-eec170784d0b")],
  [["chocolate"], img("1481391243133-f96216dcb5d2")],
  [["nut", "almond", "cashew", "peanut"], img("1508061253366-f7da5ffa58f4")],
  [["popcorn"], img("1585238342024-b62e07f37dad")],
  [["snack", "namkeen", "mixture"], img("1599490659213-e2c6c2bdb590")],

  // Frozen
  [["ice cream", "kulfi"], img("1551024601-bec78aea704b")],
  [["frozen"], img("1584568694244-14fbdf83bd30")],

  // Miscellaneous
  [["pizza"], img("1565299624946-b28f40a0ae38")],
  [["burger"], img("1568901346375-23c9450c58cd")],
  [["sandwich"], img("1481070414801-51fd732d7184")],
  [["soup"], img("1547592166-23ac45744acd")],
  [["tofu", "soy"], img("1542012324-d2c6c3c81b6d")],
];

// Category name → fallback image
const categoryImages: Record<string, string> = {
  dairy: img("1628088062854-d1870b14eb29"),
  fruits: img("1619566636858-adf3ef46400b"),
  vegetables: img("1540420773420-3366772f4999"),
  meat: img("1607623814075-e51df1bdc82f"),
  "bakery item": img("1509440159596-0249088772ff"),
  snacks: img("1599490659213-e2c6c2bdb590"),
  grains: img("1536304929831-ee1ca9d44c28"),
  beverages: img("1544145945-f90425340c7e"),
  condiments: img("1472476443507-c7a5948772fc"),
  frozen: img("1584568694244-14fbdf83bd30"),
  general: img("1606787366850-de6330128bfc"),
};

const defaultImage = img("1606787366850-de6330128bfc");

const PRODUCT_IMAGES_KEY = "foodtrack_product_images";

export function imageByName(name: string, categoryName?: string): string {
  const n = name.toLowerCase().trim();

  // Check user-uploaded images first
  try {
    const stored: Record<string, string> = JSON.parse(localStorage.getItem(PRODUCT_IMAGES_KEY) || "{}");
    if (stored[n]) return stored[n];
  } catch {
    // ignore parse errors
  }

  for (const [keywords, url] of productImages) {
    if (keywords.some((k) => n.includes(k))) return url;
  }

  if (categoryName) {
    const cat = categoryName.toLowerCase();
    if (categoryImages[cat]) return categoryImages[cat];
  }

  return defaultImage;
}
