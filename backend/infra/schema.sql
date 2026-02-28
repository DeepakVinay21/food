-- PostgreSQL production schema

create table users (
    id uuid primary key,
    email varchar(320) not null,
    password_hash text not null,
    role varchar(20) not null default 'User',
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    deleted_at_utc timestamptz null
);
create unique index ux_users_email on users (lower(email));

create table categories (
    id uuid primary key,
    name varchar(100) not null,
    normalized_name varchar(100) not null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    deleted_at_utc timestamptz null
);
create unique index ux_categories_normalized_name on categories (normalized_name);

create table products (
    id uuid primary key,
    user_id uuid not null references users(id),
    category_id uuid not null references categories(id),
    name varchar(150) not null,
    normalized_name varchar(150) not null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    deleted_at_utc timestamptz null
);
create unique index ux_products_user_name on products (user_id, normalized_name) where deleted_at_utc is null;
create index ix_products_user_id on products (user_id);

create table product_batches (
    id uuid primary key,
    product_id uuid not null references products(id),
    expiry_date date not null,
    quantity int not null check (quantity >= 0),
    status varchar(20) not null,
    notify_7_days_sent boolean not null default false,
    notify_1_day_sent boolean not null default false,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    deleted_at_utc timestamptz null
);
create index ix_product_batches_product_expiry_status on product_batches (product_id, expiry_date, status);
create index ix_product_batches_active_expiry on product_batches (expiry_date) where status = 'Active' and deleted_at_utc is null;

create table notifications (
    id uuid primary key,
    user_id uuid not null references users(id),
    product_batch_id uuid not null references product_batches(id),
    notification_type varchar(30) not null,
    sent_at_utc timestamptz not null default now(),
    success boolean not null,
    error_message text null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    deleted_at_utc timestamptz null
);
create unique index ux_notifications_batch_type on notifications (product_batch_id, notification_type);
create index ix_notifications_user_sent_at on notifications (user_id, sent_at_utc desc);

create table recipes (
    id uuid primary key,
    name varchar(150) not null,
    instructions text null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    deleted_at_utc timestamptz null
);

create table recipe_ingredients (
    id uuid primary key,
    recipe_id uuid not null references recipes(id),
    ingredient_name varchar(120) not null,
    normalized_ingredient_name varchar(120) not null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    deleted_at_utc timestamptz null
);
create index ix_recipe_ingredients_recipe on recipe_ingredients (recipe_id);
create index ix_recipe_ingredients_normalized on recipe_ingredients (normalized_ingredient_name);
